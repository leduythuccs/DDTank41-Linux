using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DbMigrate;

/// <summary>
/// "analyze &lt;tsql dir&gt; [names file]": parses the exported T-SQL and counts the statement types, functions and
/// other constructs used, to scope the translator. The names file lists "Db|Procedure" lines to include.
/// </summary>
public static class Analyze
{
    public static int Run(string tsqlDir, string namesFile)
    {
        HashSet<string> only = namesFile == null ? null
            : File.ReadAllLines(namesFile).Select(l => l.Replace('|', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> nodes = new();
        Dictionary<string, int> functions = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> where = new();
        int files = 0, failed = 0;
        foreach (string file in Directory.GetFiles(tsqlDir, "*.sql", SearchOption.AllDirectories).OrderBy(f => f))
        {
            string db = Path.GetFileName(Path.GetDirectoryName(file));
            string name = Path.GetFileName(file).Split('.')[0];
            if (only != null && !only.Contains(db + "/" + name))
            {
                continue;
            }
            files++;
            TSql160Parser parser = new TSql160Parser(true);
            TSqlFragment tree = parser.Parse(new StringReader(File.ReadAllText(file)), out IList<ParseError> errors);
            if (errors.Count > 0)
            {
                failed++;
                Console.WriteLine($"PARSE ERROR {db}/{name}: {errors[0].Message} (line {errors[0].Line})");
                continue;
            }
            Visitor v = new Visitor();
            tree.Accept(v);
            foreach (string n in v.Nodes)
            {
                nodes[n] = nodes.GetValueOrDefault(n) + 1;
                if (!where.TryGetValue(n, out HashSet<string> set))
                {
                    where[n] = set = new HashSet<string>();
                }
                set.Add(db + "/" + name);
            }
            foreach (string f in v.Functions)
            {
                functions[f] = functions.GetValueOrDefault(f) + 1;
            }
        }
        Console.WriteLine($"{files} files, {failed} parse errors");
        Console.WriteLine("\n== node types (procedures using them)");
        foreach (var kv in where.OrderByDescending(k => k.Value.Count))
        {
            string sample = kv.Value.Count <= 3 ? "  " + string.Join(", ", kv.Value) : "";
            Console.WriteLine($"{kv.Value.Count,5} {kv.Key}{sample}");
        }
        Console.WriteLine("\n== functions (calls)");
        foreach (var kv in functions.OrderByDescending(k => k.Value))
        {
            Console.WriteLine($"{kv.Value,5} {kv.Key}");
        }
        return 0;
    }

    private sealed class Visitor : TSqlFragmentVisitor
    {
        public readonly HashSet<string> Nodes = new();
        public readonly List<string> Functions = new();

        public override void Visit(TSqlFragment node)
        {
            string type = node.GetType().Name;
            if (node is TSqlStatement || node is ScalarExpression || node is TableReference || node is BooleanExpression
                || node is TableHint || node is QueryExpression || node is OptimizerHint || node is DataTypeReference)
            {
                if (node is SqlDataTypeReference dt)
                {
                    type += ":" + dt.SqlDataTypeOption;
                }
                if (node is GlobalVariableExpression g)
                {
                    type += ":" + g.Name.ToUpperInvariant();
                }
                if (node is BinaryExpression b)
                {
                    type += ":" + b.BinaryExpressionType;
                }
                Nodes.Add(type);
            }
            if (node is FunctionCall f)
            {
                Functions.Add(f.FunctionName.Value.ToUpperInvariant());
            }
            base.Visit(node);
        }
    }
}
