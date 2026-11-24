using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DbMigrate;

/// <summary>
/// "routines &lt;tsql dir&gt; [out dir] [used names file]": translates the exported procedures, functions and views
/// (see the "tsql" mode) into &lt;out&gt;/&lt;Db&gt;.routines.sql. A hand-written &lt;out&gt;/manual/&lt;Db&gt;/&lt;Name&gt;.sql
/// replaces the translation of that routine. Routines that cannot be translated are left out and listed in
/// &lt;out&gt;/routines-report.txt with the reason; warnings point at places to check by hand.
/// </summary>
public static class Routines
{
    private sealed class Module
    {
        public string Db;
        public string Name;
        public string Kind;      // P, V, FN, IF, TF
        public TSqlStatement Statement;
    }

    public static int Run(string tsqlDir, string outDir, string usedFile)
    {
        HashSet<string> used = usedFile == null ? new() : File.ReadAllLines(usedFile).Select(l => l.Replace('|', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Schema schema = new();
        foreach (string db in Program.Databases)
        {
            using SqlConnection conn = new SqlConnection(Program.ConnectionString(db));
            conn.Open();
            schema.AddTables(db, Catalog.ReadTables(conn));
        }

        List<Module> modules = new();
        foreach (string db in Program.Databases)
        {
            schema.Procedures[db] = new(StringComparer.OrdinalIgnoreCase);
            schema.Functions[db] = new(StringComparer.OrdinalIgnoreCase);
            string dir = Path.Combine(tsqlDir, db);
            foreach (string file in Directory.GetFiles(dir, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
            {
                string[] parts = Path.GetFileName(file).Split('.');
                // A T-SQL rewrite in <out>/manual-tsql/<Db>/<Name>.sql replaces the original (it is still translated).
                string rewrite = Path.Combine(outDir, "manual-tsql", db, parts[0] + ".sql");
                string text = File.ReadAllText(File.Exists(rewrite) ? rewrite : file);
                TSql160Parser parser = new TSql160Parser(true);
                TSqlFragment tree = parser.Parse(new StringReader(text), out IList<ParseError> errors);
                TSqlStatement statement = (tree as TSqlScript)?.Batches.SelectMany(b => b.Statements).FirstOrDefault();
                if (errors.Count > 0 || statement == null)
                {
                    Console.Error.WriteLine($"parse error {db}/{parts[0]}: {errors.FirstOrDefault()?.Message}");
                    continue;
                }
                Module m = new Module { Db = db, Name = parts[0], Kind = parts[1], Statement = statement };
                modules.Add(m);
                switch (statement)
                {
                    case CreateProcedureStatement p:
                        schema.Procedures[db][m.Name] = p.Parameters.Select(x => new Schema.Param
                        {
                            Name = x.VariableName.Value.TrimStart('@'),
                            Output = x.Modifier == ParameterModifier.Output,
                            Default = x.Value,
                        }).ToList();
                        break;
                    case CreateFunctionStatement f when f.ReturnType is ScalarFunctionReturnType rt:
                        schema.Functions[db][m.Name] = Translator.TypeOf(rt.DataType);
                        break;
                }
            }
        }

        StringBuilder report = new();
        int ok = 0, manual = 0, failed = 0, usedFailed = 0, warned = 0;
        foreach (string db in Program.Databases)
        {
            StringBuilder views = new(), functions = new(), procedures = new();
            List<(string Name, string Sql)> viewList = new();
            foreach (Module m in modules.Where(x => x.Db == db))
            {
                string key = db + "/" + m.Name;
                string tag = used.Contains(key) ? "USED   " : "unused ";
                string manualFile = Path.Combine(outDir, "manual", db, m.Name + ".sql");
                if (File.Exists(manualFile))
                {
                    (m.Kind == "P" ? procedures : m.Kind == "V" ? views : functions).AppendLine(File.ReadAllText(manualFile).TrimEnd()).AppendLine();
                    report.AppendLine($"manual  {tag}{key}");
                    manual++;
                    continue;
                }
                Translator tr = new Translator(schema, db);
                try
                {
                    switch (m.Statement)
                    {
                        case CreateProcedureStatement p:
                            procedures.AppendLine(tr.Procedure(p));
                            break;
                        case CreateFunctionStatement f:
                            functions.AppendLine(tr.Function(f));
                            break;
                        case CreateViewStatement v:
                            viewList.Add((m.Name, tr.View(v)));
                            break;
                        default:
                            throw new UnsupportedException("module " + m.Statement.GetType().Name);
                    }
                    ok++;
                    foreach (string w in tr.Warnings.Distinct())
                    {
                        report.AppendLine($"warning {tag}{key}: {w}");
                        warned++;
                    }
                }
                catch (UnsupportedException e)
                {
                    report.AppendLine($"FAILED  {tag}{key}: {e.Message}");
                    failed++;
                    if (used.Contains(key))
                    {
                        usedFailed++;
                    }
                }
            }
            foreach ((string _, string sql) in SortViews(viewList))
            {
                views.AppendLine(sql);
            }
            string path = Path.Combine(outDir, db + ".routines.sql");
            File.WriteAllText(path, "-- Generated by tools/DbMigrate (routines) from the SQL Server database " + db + ". Do not edit by hand:\n"
                + "-- put a replacement in manual/" + db + "/<Name>.sql instead.\n"
                + "USE " + Program.Q(db) + ";\nDELIMITER $$\n\n" + views + functions + procedures + "DELIMITER ;\n", new UTF8Encoding(false));
        }
        File.WriteAllText(Path.Combine(outDir, "routines-report.txt"), report.ToString());
        Console.WriteLine($"translated {ok}, manual {manual}, failed {failed} (used by the code: {usedFailed}), warnings {warned}");
        return 0;
    }

    /// <summary>Views that select from other views must be created after them.</summary>
    private static IEnumerable<(string Name, string Sql)> SortViews(List<(string Name, string Sql)> views)
    {
        List<(string Name, string Sql)> done = new();
        List<(string Name, string Sql)> left = new(views);
        while (left.Count > 0)
        {
            var next = left.FirstOrDefault(v => !left.Any(o => o.Name != v.Name && v.Sql.Contains("`" + o.Name + "`", StringComparison.OrdinalIgnoreCase)));
            if (next.Name == null)
            {
                next = left[0];
            }
            done.Add(next);
            left.Remove(next);
        }
        return done;
    }
}
