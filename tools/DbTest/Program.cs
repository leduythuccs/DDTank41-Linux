using System.Collections;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using log4net.Appender;
using log4net.Core;

namespace DbTest;

/// <summary>
/// Differential test of the database layer. run.sh builds this program twice: against the SQL Server version of
/// Bussiness and against the MariaDB one, then runs both from the same starting data and compares the output.
///
///   DbTest calls &lt;out.jsonl&gt;   calls every public method of every *Bussiness class, in a fixed order, with
///                                arguments made up from the parameter names (an ID of the test user, a real item
///                                template, ...), and writes one line per call: return value, ref/out values,
///                                errors logged during the call;
///   DbTest dump &lt;out.json&gt;     a hash and row count of every table (to compare what the calls wrote).
///
/// The connection strings come from DbTest.dll.config (conString, crosszoneString, membershipDb), and DBTEST_KIND
/// (mssql or mariadb) says which provider "dump" and the seed queries use.
/// </summary>
public static class Program
{
    private static readonly string[] Databases = { "Project_Player34", "Project_Game34", "Db_Membership" };
    private static readonly DateTime Fixed = new DateTime(2026, 1, 1, 12, 0, 0);
    private static readonly List<string> errors = new();
    private static string kind;
    private static readonly Dictionary<string, object> seeds = new(StringComparer.OrdinalIgnoreCase);

    public static int Main(string[] args)
    {
        kind = Environment.GetEnvironmentVariable("DBTEST_KIND") ?? throw new Exception("set DBTEST_KIND=mssql|mariadb");
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CaptureErrors();
        switch (args[0])
        {
            case "calls":
                return Calls(args[1], args.Length > 2 ? args[2] : null);
            case "dump":
                return Dump(args[1]);
            default:
                Console.Error.WriteLine("usage: DbTest calls <out.jsonl> [method filter] | dump <out.json>");
                return 2;
        }
    }

    // ------------------------------------------------------------------ errors

    /// <summary>Errors from the code under test: log4net ERROR events, and SQL Server command errors (the SQL Server
    /// version writes those to the Windows event log only, which does nothing on Linux).</summary>
    private static void CaptureErrors()
    {
        var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)log4net.LogManager.GetRepository(Assembly.GetEntryAssembly());
        hierarchy.Root.AddAppender(new ListAppender());
        hierarchy.Root.Level = Level.Warn;
        hierarchy.Configured = true;
        DiagnosticListener.AllListeners.Subscribe(new Observer<DiagnosticListener>(listener =>
        {
            if (listener.Name == "SqlClientDiagnosticListener")
            {
                listener.Subscribe(new Observer<KeyValuePair<string, object>>(e =>
                {
                    if (e.Key.EndsWith("Error") && e.Value?.GetType().GetProperty("Exception")?.GetValue(e.Value) is Exception ex)
                    {
                        lock (errors) errors.Add("sql: " + ex.Message);
                    }
                }));
            }
        }));
    }

    private sealed class ListAppender : AppenderSkeleton
    {
        protected override void Append(LoggingEvent e)
        {
            if (e.Level >= Level.Error)
            {
                string text = e.RenderedMessage + (e.ExceptionObject != null ? " | " + e.ExceptionObject.GetType().Name + ": " + e.ExceptionObject.Message : "");
                lock (errors) errors.Add(e.LoggerName + ": " + text);
            }
        }
    }

    private sealed class Observer<TValue> : IObserver<TValue>
    {
        private readonly Action<TValue> next;
        public Observer(Action<TValue> next) => this.next = next;
        public void OnNext(TValue value) => next(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    // ------------------------------------------------------------------ calls

    private static int Calls(string outFile, string filter)
    {
        Assembly bussiness = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Bussiness")
            ?? Assembly.Load("Bussiness");
        Seed(bussiness);

        List<MethodInfo> methods = bussiness.GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !t.IsAbstract && t.Name.EndsWith("Bussiness") && t.Namespace == "Bussiness")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && !m.IsGenericMethod && m.Name != "Dispose" && Callable(m))
            .Where(m => filter == null || (m.DeclaringType.Name + "." + m.Name).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => Destructive(m.Name) ? 1 : 0)
            .ThenBy(m => Key(m), StringComparer.Ordinal)
            .ToList();

        using StreamWriter w = new StreamWriter(outFile, false, new UTF8Encoding(false));
        int n = 0;
        foreach (MethodInfo m in methods)
        {
            JsonObject line;
            try
            {
                line = Call(m);
            }
            catch (Exception ex)
            {
                line = new JsonObject { ["call"] = Key(m), ["exception"] = "DbTest: " + ex.GetType().Name + ": " + ex.Message };
            }
            w.WriteLine(line.ToJsonString());
            w.Flush();
            n++;
        }
        Console.WriteLine($"{kind}: {n} calls -> {outFile}");
        return 0;
    }

    private static bool Destructive(string name) =>
        new[] { "Delete", "Remove", "Clear", "Reset", "Disband", "Drop", "Truncate", "Kill" }.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

    private static string Key(MethodInfo m) =>
        m.DeclaringType.Name + "." + m.Name + "(" + string.Join(",", m.GetParameters().Select(p => TypeName(p.ParameterType) + " " + p.Name)) + ")";

    private static string TypeName(Type t)
    {
        t = t.IsByRef ? t.GetElementType() : t;
        // The SQL Server version passes SqlDataReader where the MariaDB one passes DbDataReader: same method.
        return t.Name;
    }

    private static bool Callable(MethodInfo m) => m.GetParameters().All(p =>
    {
        Type t = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
        return !typeof(DbDataReader).IsAssignableFrom(t) && !typeof(System.Data.IDataReader).IsAssignableFrom(t)
            && !typeof(System.Data.DataTable).IsAssignableFrom(t) && !typeof(System.Data.DataSet).IsAssignableFrom(t)
            && !typeof(Delegate).IsAssignableFrom(t) && t != typeof(object);
    });

    private static JsonObject Call(MethodInfo m)
    {
        ParameterInfo[] ps = m.GetParameters();
        object[] args = ps.Select(p => Value(p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType, p.Name, 0)).ToArray();
        lock (errors) errors.Clear();
        object target = null;
        object ret = null;
        string exception = null;
        Stopwatch sw = Stopwatch.StartNew();
        Thread thread = new Thread(() =>
        {
            try
            {
                target = m.IsStatic ? null : Activator.CreateInstance(m.DeclaringType);
                ret = m.Invoke(target, args);
            }
            catch (TargetInvocationException ex)
            {
                exception = ex.InnerException.GetType().Name + ": " + ex.InnerException.Message;
            }
            catch (Exception ex)
            {
                exception = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                (target as IDisposable)?.Dispose();
            }
        });
        thread.IsBackground = true;
        thread.Start();
        bool done = thread.Join(TimeSpan.FromSeconds(30));
        JsonObject o = new JsonObject { ["call"] = Key(m) };
        if (!done)
        {
            o["timeout"] = true;
            return o;
        }
        o["ms"] = sw.ElapsedMilliseconds;
        o["ret"] = Ser(ret, 0);
        JsonArray refs = new JsonArray();
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].ParameterType.IsByRef || (!ps[i].ParameterType.IsValueType && ps[i].ParameterType != typeof(string)))
            {
                refs.Add(new JsonObject { ["name"] = ps[i].Name, ["value"] = Ser(args[i], 0) });
            }
        }
        if (refs.Count > 0)
        {
            o["refs"] = refs;
        }
        if (exception != null)
        {
            o["exception"] = exception;
        }
        lock (errors)
        {
            if (errors.Count > 0)
            {
                o["errors"] = new JsonArray(errors.Distinct().Select(e => (JsonNode)JsonValue.Create(e)).ToArray());
            }
        }
        return o;
    }

    // ------------------------------------------------------------------ arguments

    /// <summary>Test accounts and real IDs, made the same way on both databases.</summary>
    private static void Seed(Assembly bussiness)
    {
        Type players = bussiness.GetType("Bussiness.PlayerBussiness");
        foreach ((string user, string nick) in new[] { ("dbtest1", "DbTest1"), ("dbtest2", "DbTest2") })
        {
            using var pb = (IDisposable)Activator.CreateInstance(players);
            MethodInfo active = players.GetMethod("ActivePlayer");
            object[] a = { null, user, "pw", true, 0, 0, "127.0.0.1", "" };
            active.Invoke(pb, a);
            MethodInfo register = players.GetMethod("RegisterPlayer");
            string style = "7008,3158,6103,5160,1142";
            object[] r = { user, "pw", nick, style, style, "", "", "", "", "", 1, "", 0 };
            register.Invoke(pb, r);
        }
        seeds["user1"] = Scalar("Project_Player34", "SELECT UserID FROM Sys_Users_Detail WHERE UserName = 'dbtest1'");
        seeds["user2"] = Scalar("Project_Player34", "SELECT UserID FROM Sys_Users_Detail WHERE UserName = 'dbtest2'");
        seeds["item1"] = Scalar("Project_Player34", "SELECT MIN(ItemID) FROM Sys_Users_Goods WHERE UserID = " + seeds["user1"]);
        Console.WriteLine($"{kind}: seed " + string.Join(", ", seeds.Select(kv => kv.Key + "=" + kv.Value)));
    }

    private static int Seed(string name) => seeds.TryGetValue(name, out object v) && v is not DBNull && v != null ? Convert.ToInt32(v) : 1;

    private static object Value(Type t, string name, int depth)
    {
        string n = (name ?? "").ToLowerInvariant();
        Type u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(int) || u == typeof(long) || u == typeof(short) || u == typeof(byte))
        {
            return Convert.ChangeType(IntFor(n), u);
        }
        if (u == typeof(double) || u == typeof(float) || u == typeof(decimal))
        {
            return Convert.ChangeType(1, u);
        }
        if (u == typeof(bool))
        {
            return true;
        }
        if (u == typeof(string))
        {
            return StringFor(n);
        }
        if (u == typeof(DateTime))
        {
            return Fixed;
        }
        if (u == typeof(Guid))
        {
            return Guid.Empty;
        }
        if (u.IsEnum)
        {
            Array values = Enum.GetValues(u);
            return values.Length > 0 ? values.GetValue(0) : Activator.CreateInstance(u);
        }
        if (u.IsArray)
        {
            Type e = u.GetElementType();
            Array a = Array.CreateInstance(e, depth < 2 ? 1 : 0);
            if (a.Length > 0)
            {
                a.SetValue(Value(e, name, depth + 1), 0);
            }
            return a;
        }
        if (u.IsGenericType && typeof(IList).IsAssignableFrom(u) && u.GetConstructor(Type.EmptyTypes) != null)
        {
            IList list = (IList)Activator.CreateInstance(u);
            if (depth < 2)
            {
                list.Add(Value(u.GetGenericArguments()[0], name, depth + 1));
            }
            return list;
        }
        if (u.IsGenericType && typeof(IDictionary).IsAssignableFrom(u) && u.GetConstructor(Type.EmptyTypes) != null)
        {
            return Activator.CreateInstance(u);
        }
        if (u.IsClass && u.GetConstructor(Type.EmptyTypes) != null && depth < 2)
        {
            object o = Activator.CreateInstance(u);
            foreach (PropertyInfo p in u.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite && p.GetIndexParameters().Length == 0))
            {
                try
                {
                    p.SetValue(o, Value(p.PropertyType, p.Name, depth + 1));
                }
                catch
                {
                }
            }
            foreach (FieldInfo f in u.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsInitOnly))
            {
                f.SetValue(o, Value(f.FieldType, f.Name, depth + 1));
            }
            return o;
        }
        return u.IsValueType ? Activator.CreateInstance(u) : null;
    }

    private static int IntFor(string n)
    {
        if (n is "receiverid" or "toid" or "friendid" or "targetid" or "touserid" or "playerid2" or "userid2" or "buyerid" or "spouseid")
            return Seed("user2");
        if (n.Contains("userid") || n is "id" or "playerid" or "uid" or "senderid" or "auctioneerid" or "selfid" or "masterid" or "chairmanid")
            return Seed("user1");
        if (n is "itemid" || n.EndsWith("itemid") && !n.Contains("template"))
            return Seed("item1");
        if (n.Contains("templateid") || n is "goodsid" or "armid")
            return 7008;
        if (n.Contains("pagesize") || n is "size" or "count1" or "top" or "maxcount")
            return 10;
        if (n.Contains("page"))
            return 1;
        if (n.Contains("money") || n.Contains("gold") || n.Contains("price") || n.Contains("gifttoken") || n.Contains("medal"))
            return 100;
        if (n is "place" or "bagtype" or "type2" or "state" or "order" or "sex")
            return 0;
        return 1;
    }

    private static string StringFor(string n)
    {
        if (n.Contains("nick"))
            return "DbTest1";
        if (n.Contains("user") && n.Contains("name") || n is "name" or "loginname" or "account" or "username")
            return "dbtest1";
        if (n.Contains("pass") || n.Contains("pwd"))
            return "pw";
        if (n.Contains("ip"))
            return "127.0.0.1";
        if (n.Contains("ids") || n.Contains("list") || n.Contains("str"))
            return "1,2";
        if (n.Contains("date") || n.Contains("time"))
            return "2026-01-01";
        return "1";
    }

    // ------------------------------------------------------------------ output

    private static JsonNode Ser(object o, int depth)
    {
        switch (o)
        {
            case null:
            case DBNull:
                return null;
            case string s:
                return JsonValue.Create(s);
            case bool b:
                return JsonValue.Create(b);
            case DateTime d:
                // Times taken from the clock during the run differ between the two runs.
                return JsonValue.Create(Math.Abs((d - DateTime.Now).TotalDays) < 2 ? "<now>" : d.ToString("yyyy-MM-dd HH:mm:ss"));
            case float f:
                return JsonValue.Create(Math.Round(f, 4));
            case double db:
                return JsonValue.Create(Math.Round(db, 4));
            case decimal m:
                return JsonValue.Create(m / 1.0000000000000000000000000000m);
            case Enum e:
                return JsonValue.Create(e.ToString());
            case Guid g:
                return JsonValue.Create(g == Guid.Empty ? "<empty guid>" : "<guid>");
            case byte[] bytes:
                return JsonValue.Create("bytes:" + Convert.ToHexString(bytes.Take(64).ToArray()));
        }
        Type t = o.GetType();
        if (t.IsPrimitive)
        {
            return JsonValue.Create(Convert.ToInt64(o));
        }
        if (depth > 3)
        {
            return JsonValue.Create("<" + t.Name + ">");
        }
        if (o is IDictionary dict)
        {
            JsonObject r = new JsonObject();
            List<(string Key, object Value)> entries = new();
            IDictionaryEnumerator it = dict.GetEnumerator();
            while (it.MoveNext())
            {
                entries.Add((Convert.ToString(it.Key, CultureInfo.InvariantCulture), it.Value));
            }
            foreach ((string key, object value) in entries.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                r[key] = Ser(value, depth + 1);
            }
            return r;
        }
        if (o is IEnumerable seq)
        {
            JsonArray a = new JsonArray();
            int count = 0;
            foreach (object item in seq)
            {
                if (++count > 100)
                {
                    a.Add(JsonValue.Create("..."));
                    break;
                }
                a.Add(Ser(item, depth + 1));
            }
            return a;
        }
        JsonObject obj = new JsonObject();
        foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            object v;
            try
            {
                v = p.GetValue(o);
            }
            catch (Exception ex)
            {
                v = "<" + (ex.InnerException ?? ex).GetType().Name + ">";
            }
            obj[p.Name] = Ser(v, depth + 1);
        }
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            obj[f.Name] = Ser(f.GetValue(o), depth + 1);
        }
        return obj;
    }

    // ------------------------------------------------------------------ database access (seeds, dump)

    private static DbConnection Open(string db)
    {
        string key = db switch { "Project_Game34" => "crosszoneString", "Db_Membership" => "membershipDb", _ => "conString" };
        string cs = System.Configuration.ConfigurationManager.AppSettings[key];
        DbConnection c = kind == "mssql" ? new Microsoft.Data.SqlClient.SqlConnection(cs) : new MySqlConnector.MySqlConnection(cs);
        c.Open();
        return c;
    }

    private static object Scalar(string db, string sql)
    {
        using DbConnection c = Open(db);
        using DbCommand cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    /// <summary>Row count and content hash of every table; values normalised so both databases print them alike.</summary>
    private static int Dump(string outFile)
    {
        JsonObject result = new JsonObject();
        foreach (string db in Databases)
        {
            using DbConnection c = Open(db);
            List<string> tables = new();
            using (DbCommand cmd = c.CreateCommand())
            {
                cmd.CommandText = kind == "mssql"
                    ? "SELECT name FROM sys.tables WHERE is_ms_shipped = 0"
                    : "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'";
                using DbDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    tables.Add(r.GetString(0));
                }
            }
            foreach (string table in tables.Where(t => !t.StartsWith('_')).OrderBy(t => t.ToLowerInvariant(), StringComparer.Ordinal))
            {
                using DbCommand cmd = c.CreateCommand();
                cmd.CommandText = kind == "mssql" ? $"SELECT * FROM [{table}]" : $"SELECT * FROM `{table}`";
                using DbDataReader r = cmd.ExecuteReader();
                string[] names = Enumerable.Range(0, r.FieldCount).Select(i => r.GetName(i).ToLowerInvariant()).ToArray();
                int[] order = Enumerable.Range(0, names.Length).OrderBy(i => names[i], StringComparer.Ordinal).ToArray();
                List<string> rows = new();
                while (r.Read())
                {
                    rows.Add(string.Join("|", order.Select(i => Cell(r.GetValue(i)))));
                }
                rows.Sort(StringComparer.Ordinal);
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
                result[db + "." + table.ToLowerInvariant()] = new JsonObject
                {
                    ["rows"] = rows.Count,
                    ["hash"] = hash,
                    ["columns"] = string.Join(",", order.Select(i => names[i])),
                    // A few rows, to see what differs when the hashes do.
                    ["sample"] = rows.Count <= 200 ? new JsonArray(rows.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()) : null,
                };
            }
        }
        File.WriteAllText(outFile, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{kind}: {result.Count} tables -> {outFile}");
        return 0;
    }

    private static string Cell(object v) => v switch
    {
        null or DBNull => "NULL",
        bool b => b ? "1" : "0",
        sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToInt64(v).ToString(CultureInfo.InvariantCulture),
        DateTime d => Math.Abs((d - DateTime.Now).TotalDays) < 2 ? "<now>" : d.ToString("yyyy-MM-dd HH:mm:ss"),
        decimal m => (m / 1.0000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
        double db => Math.Round(db, 4).ToString(CultureInfo.InvariantCulture),
        float f => Math.Round(f, 4).ToString(CultureInfo.InvariantCulture),
        Guid g => g.ToString(),
        byte[] bytes => Convert.ToHexString(bytes),
        string s => s.TrimEnd(),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture),
    };
}
