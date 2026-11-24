using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.Data.SqlClient;
using MySqlConnector;

namespace SqlDataProvider.BaseClass
{
    /// <summary>
    /// Runs the game's stored procedures on MariaDB (see tools/DbMigrate for how they were translated).
    /// The callers still build SqlParameter objects ("@Name", output and return-value directions); they are mapped
    /// onto the translated procedure's parameters:
    ///   @Name                          -> p_Name
    ///   Direction = ReturnValue        -> OUT p_ReturnValue (the T-SQL RETURN value)
    /// and output values are copied back into the SqlParameter objects, converted to the parameter's declared type
    /// (callers unbox them: (int)param.Value). Readers convert computed values to SQL Server's types, see CoercingDataReader.
    /// </summary>
    public sealed class Sql_DbObject : IDisposable
    {
        private sealed class RoutineParam
        {
            public string Name;
            public ParameterDirection Direction;
            public string Type;    // DTD_IDENTIFIER, e.g. int(11), tinyint(1), varchar(100)
        }

        private sealed class Binding
        {
            public SqlParameter Source;
            public MySqlParameter Target;
            public string Type;
        }

        private static readonly ConcurrentDictionary<string, RoutineParam[]> Signatures = new(StringComparer.OrdinalIgnoreCase);

        private readonly string connectionString;

        public Sql_DbObject()
        {
        }

        public Sql_DbObject(string Path_Source, string Conn_DB)
        {
            connectionString = Path_Source switch
            {
                "AppConfig" => ConfigurationManager.AppSettings[Conn_DB],
                "WebConfig" => ConfigurationManager.ConnectionStrings[Conn_DB].ConnectionString,
                _ => Conn_DB,
            };
        }

        private MySqlConnection Open()
        {
            try
            {
                MySqlConnection conn = new MySqlConnection(connectionString);
                conn.Open();
                return conn;
            }
            catch (Exception ex) when (ex is MySqlException or InvalidOperationException or ArgumentException)
            {
                ApplicationLog.WriteError("Cannot open the database connection: " + ex.Message);
                return null;
            }
        }

        // ---------------------------------------------------------------- procedure calls

        /// <summary>The parameters of a procedure, read once from information_schema.</summary>
        private static RoutineParam[] Signature(MySqlConnection conn, string procedure)
        {
            return Signatures.GetOrAdd(conn.Database + "." + procedure, _ =>
            {
                using MySqlCommand cmd = new MySqlCommand(@"SELECT PARAMETER_NAME, PARAMETER_MODE, DTD_IDENTIFIER FROM information_schema.PARAMETERS
                    WHERE SPECIFIC_SCHEMA = DATABASE() AND SPECIFIC_NAME = @p AND ROUTINE_TYPE = 'PROCEDURE' ORDER BY ORDINAL_POSITION", conn);
                cmd.Parameters.AddWithValue("@p", procedure);
                List<RoutineParam> list = new();
                using MySqlDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    list.Add(new RoutineParam
                    {
                        Name = r.GetString(0),
                        Direction = r.GetString(1) switch
                        {
                            "OUT" => ParameterDirection.Output,
                            "INOUT" => ParameterDirection.InputOutput,
                            _ => ParameterDirection.Input,
                        },
                        Type = r.GetString(2).ToLowerInvariant(),
                    });
                }
                return list.ToArray();
            });
        }

        /// <summary>T-SQL names as the callers write them: "[SP_X]", "dbo.SP_X" or "[dbo].[SP_X]" -> "SP_X".</summary>
        private static string ProcedureName(string name)
        {
            string n = name.Trim().Replace("[", "").Replace("]", "");
            int dot = n.LastIndexOf('.');
            return dot >= 0 ? n[(dot + 1)..] : n;
        }

        private static MySqlCommand ProcedureCommand(MySqlConnection conn, string procedure, SqlParameter[] parameters, List<Binding> outputs, int commandTimeout = -1)
        {
            procedure = ProcedureName(procedure);
            MySqlCommand cmd = new MySqlCommand(procedure, conn) { CommandType = CommandType.StoredProcedure };
            if (commandTimeout >= 0)
            {
                cmd.CommandTimeout = commandTimeout;
            }
            parameters ??= Array.Empty<SqlParameter>();
            foreach (RoutineParam rp in Signature(conn, procedure))
            {
                SqlParameter source = rp.Name.Equals("p_ReturnValue", StringComparison.OrdinalIgnoreCase)
                    ? parameters.FirstOrDefault(p => p.Direction == ParameterDirection.ReturnValue)
                    : parameters.FirstOrDefault(p => p.Direction != ParameterDirection.ReturnValue
                        && ("p_" + p.ParameterName.TrimStart('@')).Equals(rp.Name, StringComparison.OrdinalIgnoreCase));
                object value = rp.Direction == ParameterDirection.Output ? DBNull.Value : ToDb(source?.Value);
                MySqlParameter target = new MySqlParameter(rp.Name, value) { Direction = rp.Direction };
                cmd.Parameters.Add(target);
                if (source != null && rp.Direction != ParameterDirection.Input)
                {
                    outputs.Add(new Binding { Source = source, Target = target, Type = rp.Type });
                }
            }
            return cmd;
        }

        private static object ToDb(object value)
        {
            return value ?? DBNull.Value;
        }

        /// <summary>Output values typed as SQL Server returned them (MariaDB hands integers back as Int64).</summary>
        private static void CopyOutputs(List<Binding> outputs)
        {
            foreach (Binding b in outputs)
            {
                object v = b.Target.Value;
                if (v == null || v is DBNull)
                {
                    b.Source.Value = DBNull.Value;
                    continue;
                }
                string type = b.Type;
                b.Source.Value = type switch
                {
                    _ when type.StartsWith("tinyint(1)") => Convert.ToBoolean(v),
                    _ when type.StartsWith("int") || type.StartsWith("smallint") || type.StartsWith("mediumint") => Convert.ToInt32(v),
                    _ when type.StartsWith("tinyint") => Convert.ToByte(v),
                    _ when type.StartsWith("bigint") => Convert.ToInt64(v),
                    _ when type.StartsWith("decimal") => Convert.ToDecimal(v),
                    _ when type.StartsWith("double") => Convert.ToDouble(v),
                    _ when type.StartsWith("float") => Convert.ToSingle(v),
                    _ when type.StartsWith("datetime") || type.StartsWith("date") => Convert.ToDateTime(v),
                    _ when type.StartsWith("varchar") || type.StartsWith("char") || type.Contains("text") => Convert.ToString(v),
                    _ => v,
                };
            }
        }

        private static void LogError(string procedure, Exception ex)
        {
            ApplicationLog.WriteError("Procedure " + procedure + ": " + ex.Message.Trim());
        }

        public bool RunProcedure(string ProcedureName, SqlParameter[] SqlParameters)
        {
            using MySqlConnection conn = Open();
            if (conn == null)
            {
                return false;
            }
            try
            {
                List<Binding> outputs = new();
                using MySqlCommand cmd = ProcedureCommand(conn, ProcedureName, SqlParameters, outputs);
                cmd.ExecuteNonQuery();
                CopyOutputs(outputs);
                return true;
            }
            catch (MySqlException ex)
            {
                LogError(ProcedureName, ex);
                return false;
            }
        }

        public bool RunProcedure(string ProcedureName) => RunProcedure(ProcedureName, null);

        public void BeginRunProcedure(string ProcedureName, SqlParameter[] SqlParameters)
        {
            RunProcedure(ProcedureName, SqlParameters);
        }

        public bool GetReader(ref DbDataReader ResultDataReader, string ProcedureName) => GetReader(ref ResultDataReader, ProcedureName, null);

        /// <summary>The reader owns the connection; closing it copies the output parameters back and closes the connection.</summary>
        public bool GetReader(ref DbDataReader ResultDataReader, string ProcedureName, SqlParameter[] SqlParameters)
        {
            MySqlConnection conn = Open();
            if (conn == null)
            {
                return false;
            }
            try
            {
                List<Binding> outputs = new();
                MySqlCommand cmd = ProcedureCommand(conn, ProcedureName, SqlParameters, outputs);
                MySqlDataReader reader = cmd.ExecuteReader();
                ResultDataReader = new CoercingDataReader(reader, () =>
                {
                    CopyOutputs(outputs);
                    cmd.Dispose();
                    conn.Dispose();
                });
                return true;
            }
            catch (MySqlException ex)
            {
                LogError(ProcedureName, ex);
                conn.Dispose();
                return false;
            }
        }

        /// <summary>Runs a procedure and loads its result sets (Table, Table1, ... like SqlDataAdapter).</summary>
        private List<DataTable> LoadTables(string ProcedureName, SqlParameter[] SqlParameters, int commandTimeout, string firstName)
        {
            List<DataTable> tables = new();
            using MySqlConnection conn = Open();
            if (conn == null)
            {
                return tables;
            }
            try
            {
                List<Binding> outputs = new();
                using MySqlCommand cmd = ProcedureCommand(conn, ProcedureName, SqlParameters, outputs, commandTimeout);
                using (CoercingDataReader reader = new CoercingDataReader(cmd.ExecuteReader(), null))
                {
                    do
                    {
                        if (reader.FieldCount == 0)
                        {
                            continue;
                        }
                        tables.Add(Load(reader, tables.Count == 0 ? firstName : "Table" + tables.Count));
                    } while (reader.NextResult());
                }
                CopyOutputs(outputs);
            }
            catch (MySqlException ex)
            {
                LogError(ProcedureName, ex);
            }
            return tables;
        }

        /// <summary>One result set as a DataTable: columns typed like SqlDataAdapter made them, no constraints.</summary>
        private static DataTable Load(DbDataReader reader, string name)
        {
            DataTable table = new DataTable(name);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string column = reader.GetName(i);
                string unique = column;
                for (int n = 1; table.Columns.Contains(unique); n++)
                {
                    unique = column + n;
                }
                table.Columns.Add(unique, reader.GetFieldType(i));
            }
            object[] values = new object[reader.FieldCount];
            table.BeginLoadData();
            while (reader.Read())
            {
                reader.GetValues(values);
                table.Rows.Add(values);
            }
            table.EndLoadData();
            return table;
        }

        private static DataTable Page(DataTable table, int StartRecordNo, int PageSize)
        {
            DataTable page = table.Clone();
            foreach (DataRow row in table.Rows.Cast<DataRow>().Skip(Math.Max(StartRecordNo, 0)).Take(PageSize))
            {
                page.ImportRow(row);
            }
            return page;
        }

        public DataSet GetDataSet(string ProcedureName, SqlParameter[] SqlParameters)
        {
            DataSet dataSet = new DataSet();
            foreach (DataTable t in LoadTables(ProcedureName, SqlParameters, -1, "Table"))
            {
                dataSet.Tables.Add(t);
            }
            return dataSet;
        }

        public DataSet GetDateSet(string DatesetName, string ProcedureName, SqlParameter[] SqlParameters)
        {
            DataSet dataSet = GetDataSet(ProcedureName, SqlParameters);
            dataSet.DataSetName = DatesetName;
            return dataSet;
        }

        public bool GetDataSet(ref DataSet ResultDataSet, ref int row_total, string TableName, string ProcedureName, int StartRecordNo, int PageSize, SqlParameter[] SqlParameters)
        {
            List<DataTable> tables = LoadTables(ProcedureName, SqlParameters, -1, TableName);
            row_total = tables.Count > 0 ? tables[0].Rows.Count : 0;
            if (tables.Count > 0)
            {
                ResultDataSet.Tables.Add(Page(tables[0], StartRecordNo, PageSize));
            }
            return true;
        }

        public DataTable GetDataTable(string TableName, string ProcedureName, SqlParameter[] SqlParameters)
        {
            return GetDataTable(TableName, ProcedureName, SqlParameters, -1);
        }

        public DataTable GetDataTable(string TableName, string ProcedureName, SqlParameter[] SqlParameters, int commandTimeout)
        {
            List<DataTable> tables = LoadTables(ProcedureName, SqlParameters, commandTimeout, TableName);
            return tables.Count > 0 ? tables[0] : new DataTable(TableName);
        }

        public DataTable GetDataTable(string TableName, string ProcedureName) => GetDataTable(TableName, ProcedureName, null, -1);

        public DataTable GetDataTable(string TableName, string ProcedureName, int StartRecordNo, int PageSize)
        {
            return Page(GetDataTable(TableName, ProcedureName, null, -1), StartRecordNo, PageSize);
        }

        public DataTable GetDataTable(string TableName, string ProcedureName, SqlParameter[] SqlParameters, int StartRecordNo, int PageSize)
        {
            return Page(GetDataTable(TableName, ProcedureName, SqlParameters, -1), StartRecordNo, PageSize);
        }

        public bool GetDataTable(ref DataTable ResultTable, string TableName, string ProcedureName, int StartRecordNo, int PageSize)
        {
            ResultTable = Page(GetDataTable(TableName, ProcedureName, null, -1), StartRecordNo, PageSize);
            return true;
        }

        public bool GetDataTable(ref DataTable ResultTable, string TableName, string ProcedureName, int StartRecordNo, int PageSize, SqlParameter[] SqlParameters)
        {
            ResultTable = Page(GetDataTable(TableName, ProcedureName, SqlParameters, -1), StartRecordNo, PageSize);
            return true;
        }

        // ---------------------------------------------------------------- SQL text (MariaDB syntax)

        public bool Exesqlcomm(string Sqlcomm, SqlParameter[] SqlParameters)
        {
            string msgError = "";
            return Exesqlcomm(Sqlcomm, SqlParameters, ref msgError);
        }

        public bool Exesqlcomm(string Sqlcomm, SqlParameter[] SqlParameters, ref string msgError)
        {
            using MySqlConnection conn = Open();
            if (conn == null)
            {
                return false;
            }
            try
            {
                using MySqlCommand cmd = new MySqlCommand(Sqlcomm, conn);
                foreach (SqlParameter p in SqlParameters ?? Array.Empty<SqlParameter>())
                {
                    cmd.Parameters.AddWithValue(p.ParameterName, ToDb(p.Value));
                }
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (MySqlException ex)
            {
                msgError = "Execute sql statement: " + Sqlcomm + " Error information: " + ex.Message.Trim();
                ApplicationLog.WriteError(msgError);
                return false;
            }
        }

        public bool Exesqlcomm(string Sqlcomm) => Exesqlcomm(Sqlcomm, null);

        public int GetRecordCount(string Sqlcomm)
        {
            using MySqlConnection conn = Open();
            if (conn == null)
            {
                return 0;
            }
            try
            {
                using MySqlCommand cmd = new MySqlCommand(Sqlcomm, conn);
                object v = cmd.ExecuteScalar();
                return v == null || v is DBNull ? 0 : Convert.ToInt32(v);
            }
            catch (MySqlException ex)
            {
                ApplicationLog.WriteError("Sql " + Sqlcomm + ": " + ex.Message.Trim());
                return 0;
            }
        }

        public DataTable GetDataTableBySqlcomm(string TableName, string Sqlcomm)
        {
            using MySqlConnection conn = Open();
            if (conn == null)
            {
                return new DataTable(TableName);
            }
            try
            {
                using MySqlCommand cmd = new MySqlCommand(Sqlcomm, conn);
                using CoercingDataReader reader = new CoercingDataReader(cmd.ExecuteReader(), null);
                return Load(reader, TableName);
            }
            catch (MySqlException ex)
            {
                ApplicationLog.WriteError("Sql " + Sqlcomm + ": " + ex.Message.Trim());
                return new DataTable(TableName);
            }
        }

        public DataTable GetDataTableBySqlcomm(string TableName, string Sqlcomm, int StartRecordNo, int PageSize)
        {
            return Page(GetDataTableBySqlcomm(TableName, Sqlcomm), StartRecordNo, PageSize);
        }

        public DataSet GetDataSetBySqlcomm(string TableName, string Sqlcomm)
        {
            DataSet dataSet = new DataSet();
            dataSet.Tables.Add(GetDataTableBySqlcomm(TableName, Sqlcomm));
            return dataSet;
        }

        public bool FillSqlDataReader(ref DbDataReader Sdr, string SqlComm)
        {
            MySqlConnection conn = Open();
            if (conn == null)
            {
                return false;
            }
            try
            {
                MySqlCommand cmd = new MySqlCommand(SqlComm, conn);
                Sdr = new CoercingDataReader(cmd.ExecuteReader(), () =>
                {
                    cmd.Dispose();
                    conn.Dispose();
                });
                return true;
            }
            catch (MySqlException ex)
            {
                ApplicationLog.WriteError("Sql " + SqlComm + ": " + ex.Message.Trim());
                conn.Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
