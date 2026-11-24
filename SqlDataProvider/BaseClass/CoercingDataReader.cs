using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace SqlDataProvider.BaseClass
{
    /// <summary>
    /// A MariaDB reader that gives the game code the value types SQL Server gave it. The code unboxes directly
    /// ((int)reader["X"]), so the types must match exactly:
    ///   - computed integers (COUNT(*), a + b, bit operations) are BIGINT in MariaDB, INT in SQL Server: Int32;
    ///   - SUM over integers is DECIMAL(n,0) in MariaDB, INT in SQL Server: Int32.
    /// Columns read straight from a table keep their type (a real BIGINT column stays Int64).
    /// When the reader closes, the procedure's output parameters are copied back (see Sql_DbObject).
    /// </summary>
    public sealed class CoercingDataReader : DbDataReader
    {
        private readonly MySqlDataReader inner;
        private readonly Action onClose;
        private Type[] targets;
        private bool closed;

        public CoercingDataReader(MySqlDataReader inner, Action onClose)
        {
            this.inner = inner;
            this.onClose = onClose;
            ComputeTargets();
        }

        private void ComputeTargets()
        {
            targets = new Type[inner.FieldCount];
            ReadOnlyCollection<DbColumn> schema = inner.FieldCount > 0 ? inner.GetColumnSchema() : null;
            for (int i = 0; i < targets.Length; i++)
            {
                Type type = inner.GetFieldType(i);
                bool fromTable = !string.IsNullOrEmpty(schema?[i].BaseTableName);
                if (type == typeof(long) && !fromTable || type == typeof(ulong) || type == typeof(uint))
                {
                    type = typeof(int);
                }
                else if (type == typeof(decimal) && !fromTable && (schema?[i].NumericScale ?? 0) == 0)
                {
                    type = typeof(int);
                }
                targets[i] = type;
            }
        }

        /// <summary>A value of the column converted to its target type.</summary>
        private object Coerce(int i, object value)
        {
            if (value is DBNull || value.GetType() == targets[i])
            {
                return value;
            }
            if (targets[i] == typeof(int))
            {
                switch (value)
                {
                    case long l when l >= int.MinValue && l <= int.MaxValue:
                        return (int)l;
                    case ulong u when u <= int.MaxValue:
                        return (int)u;
                    case uint ui when ui <= int.MaxValue:
                        return (int)ui;
                    case decimal d when d >= int.MinValue && d <= int.MaxValue:
                        return (int)d;
                }
            }
            return value;
        }

        public override object GetValue(int ordinal) => Coerce(ordinal, inner.GetValue(ordinal));

        public override int GetValues(object[] values)
        {
            int n = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < n; i++)
            {
                values[i] = GetValue(i);
            }
            return n;
        }

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override Type GetFieldType(int ordinal) => targets[ordinal];

        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));

        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));

        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));

        public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));

        public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));

        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));

        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));

        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));

        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal));

        public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);

        public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);

        public override char GetChar(int ordinal) => inner.GetChar(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

        public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);

        public override string GetName(int ordinal) => inner.GetName(ordinal);

        public override int GetOrdinal(string name) => inner.GetOrdinal(name);

        public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);

        public override int FieldCount => inner.FieldCount;

        public override bool HasRows => inner.HasRows;

        public override bool IsClosed => closed || inner.IsClosed;

        public override int RecordsAffected => inner.RecordsAffected;

        public override int Depth => inner.Depth;

        public override bool Read() => inner.Read();

        public override bool NextResult()
        {
            bool more = inner.NextResult();
            if (more)
            {
                ComputeTargets();
            }
            return more;
        }

        public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);

        public override DataTable GetSchemaTable()
        {
            DataTable schema = inner.GetSchemaTable();
            if (schema != null)
            {
                for (int i = 0; i < schema.Rows.Count && i < targets.Length; i++)
                {
                    schema.Rows[i]["DataType"] = targets[i];
                }
            }
            return schema;
        }

        public override void Close()
        {
            if (closed)
            {
                return;
            }
            closed = true;
            try
            {
                // Read to the end: the output parameters come after the result sets.
                while (inner.NextResult())
                {
                }
            }
            catch (MySqlException)
            {
            }
            inner.Close();
            onClose?.Invoke();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Close();
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
