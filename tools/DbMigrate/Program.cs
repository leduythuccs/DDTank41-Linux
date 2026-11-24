using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DbMigrate;

/// <summary>
/// Reads the game databases from SQL Server and writes MariaDB scripts:
///   Database/mariadb/&lt;Db&gt;.schema.sql   tables, keys and indexes
///   Database/mariadb/&lt;Db&gt;.data.sql     the rows
/// SQL Server is only read. Usage (from the repo root, with deploy/.env loaded):
///   dotnet run --project tools/DbMigrate -- [schema|data|all] [out dir]
/// Connection: DDTANK_MSSQL (full connection string), else 127.0.0.1,1433 as ddtank / DDTANK_DB_PASSWORD.
/// </summary>
public static class Program
{
    public static readonly string[] Databases = { "Project_Player34", "Project_Game34", "Db_Membership" };

    public static int Main(string[] args)
    {
        string what = args.Length > 0 ? args[0] : "all";
        string outDir = args.Length > 1 ? args[1] : "Database/mariadb";
        Directory.CreateDirectory(outDir);
        foreach (string db in Databases)
        {
            using SqlConnection conn = new SqlConnection(ConnectionString(db));
            conn.Open();
            List<Table> tables = Catalog.ReadTables(conn);
            if (what is "schema" or "all")
            {
                File.WriteAllText(Path.Combine(outDir, db + ".schema.sql"), SchemaWriter.Write(db, tables), new UTF8Encoding(false));
                Console.WriteLine($"{db}: {tables.Count} tables -> {db}.schema.sql");
            }
            if (what is "data" or "all")
            {
                long rows = DataWriter.Write(conn, db, tables, Path.Combine(outDir, db + ".data.sql"));
                Console.WriteLine($"{db}: {rows} rows -> {db}.data.sql");
            }
        }
        return 0;
    }

    public static string ConnectionString(string db)
    {
        string cs = Environment.GetEnvironmentVariable("DDTANK_MSSQL");
        if (string.IsNullOrEmpty(cs))
        {
            cs = "Data Source=127.0.0.1,1433;User ID=ddtank;Password=" + Environment.GetEnvironmentVariable("DDTANK_DB_PASSWORD")
                + ";TrustServerCertificate=True;Encrypt=True";
        }
        return new SqlConnectionStringBuilder(cs) { InitialCatalog = db, ApplicationIntent = ApplicationIntent.ReadOnly }.ConnectionString;
    }

    /// <summary>A MariaDB identifier.</summary>
    public static string Q(string name) => "`" + name.Replace("`", "``") + "`";
}

public sealed class Column
{
    public string Name;
    public string Type;        // SQL Server type name
    public int MaxLength;      // bytes; -1 = max
    public int Precision;
    public int Scale;
    public bool Nullable;
    public bool Identity;
    public long IdentitySeed;
    public long IdentityIncrement;
    public string Default;     // SQL Server default definition, e.g. ((0))
    public bool Indexed;
    public string MariaType;   // filled by SchemaWriter
}

public sealed class Index
{
    public string Name;
    public bool Primary;
    public bool Unique;
    public bool Filtered;
    public List<(string Column, bool Desc)> Columns = new();
}

public sealed class Table
{
    public string Schema;
    public string Name;
    public List<Column> Columns = new();
    public List<Index> Indexes = new();
    public long NextIdentity;
}

public static class Catalog
{
    public static List<Table> ReadTables(SqlConnection conn)
    {
        Dictionary<int, Table> byId = new();
        using (SqlCommand cmd = new SqlCommand(@"SELECT t.object_id, s.name, t.name FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0 ORDER BY t.name", conn))
        using (SqlDataReader r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                byId[r.GetInt32(0)] = new Table { Schema = r.GetString(1), Name = r.GetString(2) };
            }
        }
        using (SqlCommand cmd = new SqlCommand(@"SELECT c.object_id, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable,
              c.is_identity, CAST(ic.seed_value AS bigint), CAST(ic.increment_value AS bigint), dc.definition
            FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
            JOIN sys.tables t ON t.object_id = c.object_id
            ORDER BY c.object_id, c.column_id", conn))
        using (SqlDataReader r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                if (!byId.TryGetValue(r.GetInt32(0), out Table t))
                {
                    continue;
                }
                t.Columns.Add(new Column
                {
                    Name = r.GetString(1),
                    Type = r.GetString(2),
                    MaxLength = r.GetInt16(3),
                    Precision = r.GetByte(4),
                    Scale = r.GetByte(5),
                    Nullable = r.GetBoolean(6),
                    Identity = r.GetBoolean(7),
                    IdentitySeed = r.IsDBNull(8) ? 1 : r.GetInt64(8),
                    IdentityIncrement = r.IsDBNull(9) ? 1 : r.GetInt64(9),
                    Default = r.IsDBNull(10) ? null : r.GetString(10),
                });
            }
        }
        using (SqlCommand cmd = new SqlCommand(@"SELECT i.object_id, i.name, i.is_primary_key, i.is_unique, i.has_filter, c.name, ic.is_descending_key
            FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = i.object_id
            WHERE i.type > 0 AND ic.is_included_column = 0
            ORDER BY i.object_id, i.index_id, ic.key_ordinal", conn))
        using (SqlDataReader r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                if (!byId.TryGetValue(r.GetInt32(0), out Table t))
                {
                    continue;
                }
                string name = r.GetString(1);
                Index index = t.Indexes.FirstOrDefault(i => i.Name == name);
                if (index == null)
                {
                    index = new Index { Name = name, Primary = r.GetBoolean(2), Unique = r.GetBoolean(3), Filtered = r.GetBoolean(4) };
                    t.Indexes.Add(index);
                }
                index.Columns.Add((r.GetString(5), r.GetBoolean(6)));
            }
        }
        foreach (Table t in byId.Values)
        {
            foreach (Index i in t.Indexes)
            {
                foreach ((string col, _) in i.Columns)
                {
                    t.Columns.First(c => c.Name == col).Indexed = true;
                }
            }
            Column id = t.Columns.FirstOrDefault(c => c.Identity);
            if (id != null)
            {
                using SqlCommand cmd = new SqlCommand("SELECT CAST(IDENT_CURRENT(@t) AS bigint), (SELECT COUNT(*) FROM " + Bracket(t) + ")", conn);
                cmd.Parameters.AddWithValue("@t", t.Schema + "." + t.Name);
                using SqlDataReader r = cmd.ExecuteReader();
                r.Read();
                long current = r.IsDBNull(0) ? id.IdentitySeed - id.IdentityIncrement : r.GetInt64(0);
                // An empty table that was never inserted into reports the seed itself as current.
                t.NextIdentity = r.GetInt32(1) == 0 && current == id.IdentitySeed ? id.IdentitySeed : current + id.IdentityIncrement;
            }
        }
        return byId.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string Bracket(Table t) => "[" + t.Schema + "].[" + t.Name.Replace("]", "]]") + "]";
}

public static class SchemaWriter
{
    /// <summary>InnoDB's row size limit for declared VARCHARs (bytes); longer rows get TEXT columns instead.</summary>
    private const int RowLimit = 60000;

    public static string Write(string db, List<Table> tables)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("-- Generated by tools/DbMigrate from the SQL Server database " + db + ". Do not edit by hand.");
        sb.AppendLine("CREATE DATABASE IF NOT EXISTS " + Program.Q(db) + " CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_as_ci;");
        sb.AppendLine("USE " + Program.Q(db) + ";");
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 0;");
        foreach (Table t in tables)
        {
            sb.AppendLine();
            sb.AppendLine("DROP TABLE IF EXISTS " + Program.Q(t.Name) + ";");
            sb.AppendLine("CREATE TABLE " + Program.Q(t.Name) + " (");
            foreach (Column c in t.Columns)
            {
                c.MariaType = MapType(c);
            }
            FitRowSize(t);
            List<string> lines = new();
            foreach (Column c in t.Columns)
            {
                string line = "  " + Program.Q(c.Name) + " " + c.MariaType + (c.Nullable ? " NULL" : " NOT NULL");
                if (c.Identity)
                {
                    line += " AUTO_INCREMENT";
                }
                else if (c.Default != null)
                {
                    string def = MapDefault(c.Default, c);
                    if (def != null)
                    {
                        line += " DEFAULT " + def;
                    }
                }
                lines.Add(line);
            }
            Index pk = t.Indexes.FirstOrDefault(i => i.Primary);
            if (pk != null)
            {
                lines.Add("  PRIMARY KEY (" + string.Join(", ", pk.Columns.Select(k => Program.Q(k.Column))) + ")");
            }
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (Index i in t.Indexes.Where(i => !i.Primary))
            {
                // A filtered unique index (WHERE col IS NOT NULL ...) cannot be expressed: keep it as a plain index.
                string kind = i.Unique && !i.Filtered ? "UNIQUE KEY" : "KEY";
                string name = UniqueName(names, i.Name);
                lines.Add("  " + kind + " " + Program.Q(name) + " (" + string.Join(", ", i.Columns.Select(k => Program.Q(k.Column) + (k.Desc ? " DESC" : ""))) + ")");
            }
            Column id = t.Columns.FirstOrDefault(c => c.Identity);
            if (id != null && !t.Indexes.Any(i => i.Columns[0].Column == id.Name))
            {
                // MariaDB needs an index starting with the AUTO_INCREMENT column.
                lines.Add("  KEY " + Program.Q(UniqueName(names, "IX_" + id.Name)) + " (" + Program.Q(id.Name) + ")");
            }
            sb.AppendLine(string.Join(",\n", lines));
            sb.Append(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_as_ci");
            if (id != null && t.NextIdentity > 1)
            {
                sb.Append(" AUTO_INCREMENT=" + t.NextIdentity);
            }
            sb.AppendLine(";");
        }
        sb.AppendLine();
        sb.AppendLine("SET FOREIGN_KEY_CHECKS = 1;");
        return sb.ToString();
    }

    private static string UniqueName(HashSet<string> names, string name)
    {
        string n = name.Length > 60 ? name[..60] : name;
        string candidate = n;
        for (int i = 2; !names.Add(candidate); i++)
        {
            candidate = n + "_" + i;
        }
        return candidate;
    }

    public static string MapType(Column c)
    {
        int chars = c.Type is "nvarchar" or "nchar" ? c.MaxLength / 2 : c.MaxLength;
        switch (c.Type)
        {
            case "int": return "INT";
            case "bigint": return "BIGINT";
            case "smallint": return "SMALLINT";
            case "tinyint": return "TINYINT UNSIGNED";
            case "bit": return "TINYINT(1)";
            case "float": return c.Precision <= 24 ? "FLOAT" : "DOUBLE";
            case "real": return "FLOAT";
            case "decimal":
            case "numeric": return "DECIMAL(" + c.Precision + "," + c.Scale + ")";
            case "money": return "DECIMAL(19,4)";
            case "smallmoney": return "DECIMAL(10,4)";
            case "datetime":
            case "datetime2":
            case "smalldatetime": return "DATETIME(3)";
            case "date": return "DATE";
            case "time": return "TIME(3)";
            case "uniqueidentifier": return "CHAR(36)";
            case "nvarchar":
            case "varchar":
                if (c.MaxLength == -1)
                {
                    return "LONGTEXT";
                }
                return chars > 255 && !c.Indexed ? "TEXT" : "VARCHAR(" + Math.Max(chars, 1) + ")";
            case "nchar":
            case "char": return "CHAR(" + Math.Max(chars, 1) + ")";
            case "ntext":
            case "text":
            case "xml": return "LONGTEXT";
            case "image": return "LONGBLOB";
            case "varbinary":
            case "binary":
                if (c.MaxLength == -1)
                {
                    return "LONGBLOB";
                }
                return c.MaxLength > 255 ? "BLOB" : (c.Type == "binary" ? "BINARY(" : "VARBINARY(") + c.MaxLength + ")";
            case "timestamp":
            case "rowversion": return "BINARY(8)";
            default: throw new NotSupportedException("column type " + c.Type + " (" + c.Name + ")");
        }
    }

    /// <summary>Declared VARCHARs count fully against InnoDB's 64 KB row limit: turn the longest into TEXT until it fits.</summary>
    private static void FitRowSize(Table t)
    {
        static int Bytes(Column c) => c.MariaType.StartsWith("VARCHAR(") ? int.Parse(c.MariaType[8..^1]) * 4 + 2 : 8;
        while (t.Columns.Sum(Bytes) > RowLimit)
        {
            Column longest = t.Columns.Where(c => c.MariaType.StartsWith("VARCHAR(") && !c.Indexed).OrderByDescending(Bytes).FirstOrDefault();
            if (longest == null)
            {
                break;
            }
            longest.MariaType = "TEXT";
        }
    }

    /// <summary>A SQL Server default definition as a MariaDB default, or null to leave it out.</summary>
    public static string MapDefault(string def, Column c)
    {
        string d = def.Trim();
        while (d.StartsWith('(') && d.EndsWith(')') && Balanced(d[1..^1]))
        {
            d = d[1..^1].Trim();
        }
        if (Regex.IsMatch(d, @"^(getdate|sysdatetime|current_timestamp)\s*(\(\s*\))?$", RegexOptions.IgnoreCase))
        {
            return c.MariaType.StartsWith("DATETIME") ? "CURRENT_TIMESTAMP(3)" : null;
        }
        if (Regex.IsMatch(d, @"^newid\s*\(\s*\)$", RegexOptions.IgnoreCase))
        {
            return "(UUID())";
        }
        Match conv = Regex.Match(d, @"^CONVERT\s*\(\s*\[?datetime\]?\s*,\s*'(\d{4})(\d{2})(\d{2})'\s*,\s*\(?\d+\)?\s*\)$", RegexOptions.IgnoreCase);
        if (conv.Success)
        {
            return "'" + conv.Groups[1].Value + "-" + conv.Groups[2].Value + "-" + conv.Groups[3].Value + " 00:00:00'";
        }
        if (Regex.IsMatch(d, @"^0x[0-9A-Fa-f]*$"))
        {
            return d.Length == 2 ? "''" : "X'" + d[2..] + "'";
        }
        Match str = Regex.Match(d, @"^N?'((?:[^']|'')*)'$", RegexOptions.Singleline);
        if (str.Success)
        {
            string v = str.Groups[1].Value.Replace("''", "'");
            if (c.MariaType.StartsWith("DATETIME") && Regex.IsMatch(v, @"^\d{4}-\d{2}-\d{2}$"))
            {
                v += " 00:00:00";
            }
            return DataWriter.Str(v);
        }
        if (Regex.IsMatch(d, @"^-?\d+(\.\d+)?$"))
        {
            return c.MariaType.StartsWith("TINYINT(1)") ? (d == "0" ? "0" : "1") : d;
        }
        // Arithmetic such as ((2008)-(10))-(11): numbers, parentheses and + - * / only.
        if (Regex.IsMatch(d, @"^[\d\s()+\-*/]+$"))
        {
            if (c.MariaType.StartsWith("DATETIME"))
            {
                // SQL Server reads a number as a datetime: days since 1900-01-01.
                double days = Convert.ToDouble(new System.Data.DataTable().Compute(d, null), CultureInfo.InvariantCulture);
                return "'" + new DateTime(1900, 1, 1).AddDays(days).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'";
            }
            return "(" + d + ")";
        }
        Console.Error.WriteLine("  default not converted: " + c.Name + " " + def);
        return null;
    }

    private static bool Balanced(string s)
    {
        int depth = 0;
        foreach (char ch in s)
        {
            depth += ch == '(' ? 1 : ch == ')' ? -1 : 0;
            if (depth < 0)
            {
                return false;
            }
        }
        return depth == 0;
    }
}

public static class DataWriter
{
    private const int BatchBytes = 512 * 1024;

    public static long Write(SqlConnection conn, string db, List<Table> tables, string path)
    {
        long total = 0;
        using StreamWriter w = new StreamWriter(path, false, new UTF8Encoding(false));
        w.WriteLine("-- Generated by tools/DbMigrate from the SQL Server database " + db + ". Do not edit by hand.");
        w.WriteLine("USE " + Program.Q(db) + ";");
        w.WriteLine("SET FOREIGN_KEY_CHECKS = 0, UNIQUE_CHECKS = 0, SQL_MODE = CONCAT(@@SQL_MODE, ',NO_AUTO_VALUE_ON_ZERO');");
        foreach (Table t in tables)
        {
            string head = "INSERT INTO " + Program.Q(t.Name) + " (" + string.Join(", ", t.Columns.Select(c => Program.Q(c.Name))) + ") VALUES\n";
            using SqlCommand cmd = new SqlCommand("SELECT " + string.Join(", ", t.Columns.Select(c => "[" + c.Name + "]")) + " FROM " + Catalog.Bracket(t), conn);
            cmd.CommandTimeout = 600;
            using SqlDataReader r = cmd.ExecuteReader();
            StringBuilder batch = new StringBuilder();
            int inBatch = 0;
            while (r.Read())
            {
                StringBuilder row = new StringBuilder("(");
                for (int i = 0; i < t.Columns.Count; i++)
                {
                    row.Append(i == 0 ? "" : ",").Append(Value(r.GetValue(i)));
                }
                row.Append(')');
                if (inBatch > 0 && batch.Length + row.Length > BatchBytes)
                {
                    w.Write(head);
                    w.Write(batch);
                    w.WriteLine(";");
                    batch.Clear();
                    inBatch = 0;
                }
                batch.Append(inBatch == 0 ? "" : ",\n").Append(row);
                inBatch++;
                total++;
            }
            if (inBatch > 0)
            {
                w.Write(head);
                w.Write(batch);
                w.WriteLine(";");
            }
        }
        w.WriteLine("SET FOREIGN_KEY_CHECKS = 1, UNIQUE_CHECKS = 1;");
        return total;
    }

    public static string Value(object v)
    {
        switch (v)
        {
            case DBNull: return "NULL";
            case bool b: return b ? "1" : "0";
            case string s: return Str(s);
            case DateTime d: return "'" + d.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'";
            case Guid g: return "'" + g.ToString("D") + "'";
            case byte[] bytes: return bytes.Length == 0 ? "''" : "X'" + Convert.ToHexString(bytes) + "'";
            case double f: return f.ToString("R", CultureInfo.InvariantCulture);
            case float f: return f.ToString("R", CultureInfo.InvariantCulture);
            case decimal m: return m.ToString(CultureInfo.InvariantCulture);
            case IFormattable n: return n.ToString(null, CultureInfo.InvariantCulture);
            default: throw new NotSupportedException("value type " + v.GetType());
        }
    }

    /// <summary>A MariaDB string literal (default sql_mode: backslash escapes).</summary>
    public static string Str(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length + 2).Append('\'');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\'': sb.Append("''"); break;
                case '\\': sb.Append("\\\\"); break;
                case '\0': sb.Append("\\0"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\x1a': sb.Append("\\Z"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.Append('\'').ToString();
    }
}
