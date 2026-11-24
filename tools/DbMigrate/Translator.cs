using System.Globalization;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DbMigrate;

/// <summary>Value types the translator tracks, enough to pick CONCAT vs +, DIV vs / and date arithmetic.</summary>
public enum T { Unknown, Null, Int, BigInt, Bit, Float, Decimal, String, DateTime, Binary }

/// <summary>Column types of every table, and the signatures of every routine, across the 3 databases.</summary>
public sealed class Schema
{
    public readonly Dictionary<string, Dictionary<string, Dictionary<string, T>>> Tables = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, Dictionary<string, List<Param>>> Procedures = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, Dictionary<string, T>> Functions = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Param
    {
        public string Name;     // T-SQL name without @
        public bool Output;
        public ScalarExpression Default;
    }

    public void AddTables(string db, List<Table> tables)
    {
        Dictionary<string, Dictionary<string, T>> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (Table t in tables)
        {
            map[t.Name] = t.Columns.ToDictionary(c => c.Name, c => Translator.TypeOf(c.Type), StringComparer.OrdinalIgnoreCase);
        }
        Tables[db] = map;
    }

    public Dictionary<string, T> FindTable(string db, string name)
    {
        return Tables.TryGetValue(db, out var map) && map.TryGetValue(name, out var cols) ? cols : null;
    }
}

/// <summary>Thrown for T-SQL the translator does not handle; the routine is then left out (and reported).</summary>
public sealed class UnsupportedException : Exception
{
    public UnsupportedException(string message) : base(message) { }
}

/// <summary>
/// Translates one T-SQL procedure, function or view into MariaDB SQL (statement terminator $$).
/// Conventions (Sql_DbObject relies on them):
///   parameters  @X -> p_X (a local name cannot shadow a column X), locals @X -> v_X;
///   every procedure gets a last parameter OUT p_ReturnValue INT: the T-SQL RETURN value (0 by default);
///   OUTPUT parameters are INOUT.
/// </summary>
public sealed class Translator
{
    private readonly Schema schema;
    private readonly string db;
    public readonly List<string> Warnings = new();

    private readonly Dictionary<string, (string Name, T Type)> vars = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> hoisted = new();
    private readonly List<string> cursors = new();
    private readonly Dictionary<string, Dictionary<string, T>> tempTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<List<(string Alias, Dictionary<string, T> Columns, string Table)>> scopes = new();
    private readonly Stack<string> loops = new();
    private bool isFunction;
    private bool hasCursor;
    private bool lastWasSelect;
    private int loopCount;
    private bool needReturnVar;

    public Translator(Schema schema, string db)
    {
        this.schema = schema;
        this.db = db;
    }

    // ---------------------------------------------------------------- routines

    public string Procedure(CreateProcedureStatement proc)
    {
        string name = proc.ProcedureReference.Name.BaseIdentifier.Value;
        List<string> ps = new();
        List<string> defaults = new();
        foreach (ProcedureParameter p in proc.Parameters)
        {
            string n = p.VariableName.Value.TrimStart('@');
            T type = TypeOf(p.DataType);
            vars["@" + n] = ("p_" + n, type);
            bool output = p.Modifier == ParameterModifier.Output;
            ps.Add((output ? "INOUT " : "IN ") + "p_" + n + " " + MapDataType(p.DataType));
            if (p.Value != null && p.Value is not NullLiteral)
            {
                defaults.Add("IF p_" + n + " IS NULL THEN SET p_" + n + " = " + Expr(p.Value).Sql + "; END IF;");
            }
        }
        ps.Add("OUT p_ReturnValue INT");
        CollectDeclarations(proc.StatementList);
        bool transactions = Contains<BeginTransactionStatement>(proc.StatementList);
        StringBuilder body = new();
        foreach (TSqlStatement s in proc.StatementList.Statements)
        {
            body.Append(Statement(s, "  "));
        }
        StringBuilder sb = new();
        sb.AppendLine("DROP PROCEDURE IF EXISTS " + Program.Q(name) + "$$");
        sb.AppendLine("CREATE PROCEDURE " + Program.Q(name) + "(" + string.Join(", ", ps) + ")");
        sb.AppendLine("proc: BEGIN");
        AppendDeclarations(sb, transactions);
        sb.AppendLine("  SET p_ReturnValue = 0;");
        foreach (string d in defaults)
        {
            sb.AppendLine("  " + d);
        }
        sb.Append(body);
        sb.AppendLine("END$$");
        return sb.ToString();
    }

    public string Function(CreateFunctionStatement fn)
    {
        isFunction = true;
        string name = fn.Name.BaseIdentifier.Value;
        if (fn.ReturnType is not ScalarFunctionReturnType ret)
        {
            throw new UnsupportedException("table-valued function");
        }
        List<string> ps = new();
        foreach (ProcedureParameter p in fn.Parameters)
        {
            string n = p.VariableName.Value.TrimStart('@');
            vars["@" + n] = ("p_" + n, TypeOf(p.DataType));
            ps.Add("p_" + n + " " + MapDataType(p.DataType));
        }
        CollectDeclarations(fn.StatementList);
        StringBuilder body = new();
        foreach (TSqlStatement s in fn.StatementList.Statements)
        {
            body.Append(Statement(s, "  "));
        }
        StringBuilder sb = new();
        sb.AppendLine("DROP FUNCTION IF EXISTS " + Program.Q(name) + "$$");
        sb.AppendLine("CREATE FUNCTION " + Program.Q(name) + "(" + string.Join(", ", ps) + ") RETURNS " + MapDataType(ret.DataType));
        sb.AppendLine("NOT DETERMINISTIC READS SQL DATA");
        sb.AppendLine("fn: BEGIN");
        AppendDeclarations(sb, false);
        sb.Append(body);
        sb.AppendLine("END$$");
        return sb.ToString();
    }

    public string View(CreateViewStatement view)
    {
        string name = view.SchemaObjectName.BaseIdentifier.Value;
        return "CREATE OR REPLACE VIEW " + Program.Q(name) + " AS\n" + Query(view.SelectStatement.QueryExpression, null).Sql + "$$\n";
    }

    private void AppendDeclarations(StringBuilder sb, bool transactions)
    {
        foreach (string d in hoisted)
        {
            sb.AppendLine("  " + d);
        }
        if (needReturnVar)
        {
            sb.AppendLine("  DECLARE v__rv INT DEFAULT 0;");
        }
        if (hasCursor)
        {
            sb.AppendLine("  DECLARE v__fetch_status INT DEFAULT 0;");
        }
        foreach (string c in cursors)
        {
            sb.AppendLine("  " + c);
        }
        if (hasCursor)
        {
            sb.AppendLine("  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v__fetch_status = -1;");
        }
        if (transactions)
        {
            // T-SQL code checks @@ERROR and rolls back; in MariaDB an error ends the procedure, so roll back here.
            sb.AppendLine("  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;");
        }
    }

    /// <summary>T-SQL declarations can appear anywhere; MariaDB wants them first. The batch is one scope, so hoisting keeps the meaning.</summary>
    private void CollectDeclarations(StatementList list)
    {
        DeclarationCollector c = new();
        list.Accept(c);
        foreach (DeclareVariableElement d in c.Variables)
        {
            string n = d.VariableName.Value.TrimStart('@');
            if (d.DataType == null)
            {
                throw new UnsupportedException("table type variable @" + n);
            }
            if (vars.ContainsKey("@" + n))
            {
                continue;
            }
            vars["@" + n] = ("v_" + n, TypeOf(d.DataType));
            hoisted.Add("DECLARE v_" + n + " " + MapDataType(d.DataType) + " DEFAULT NULL;");
        }
        foreach (DeclareTableVariableStatement t in c.TableVariables)
        {
            tempTables[t.Body.VariableName.Value] = TableColumns(t.Body.Definition);
        }
        foreach (CreateTableStatement t in c.CreateTables)
        {
            tempTables[t.SchemaObjectName.BaseIdentifier.Value] = TableColumns(t.Definition);
        }
        hasCursor = c.Cursors.Count > 0;
    }

    private static Dictionary<string, T> TableColumns(TableDefinition def)
    {
        return def.ColumnDefinitions.ToDictionary(c => c.ColumnIdentifier.Value, c => TypeOf(c.DataType), StringComparer.OrdinalIgnoreCase);
    }

    private sealed class DeclarationCollector : TSqlFragmentVisitor
    {
        public readonly List<DeclareVariableElement> Variables = new();
        public readonly List<DeclareTableVariableStatement> TableVariables = new();
        public readonly List<CreateTableStatement> CreateTables = new();
        public readonly List<DeclareCursorStatement> Cursors = new();
        public override void Visit(DeclareVariableStatement node) => Variables.AddRange(node.Declarations);
        public override void Visit(DeclareTableVariableStatement node) => TableVariables.Add(node);
        public override void Visit(CreateTableStatement node) => CreateTables.Add(node);
        public override void Visit(DeclareCursorStatement node) => Cursors.Add(node);
    }

    private static bool Contains<TNode>(TSqlFragment fragment) where TNode : TSqlFragment
    {
        Finder<TNode> f = new();
        fragment.Accept(f);
        return f.Found;
    }

    private sealed class Finder<TNode> : TSqlFragmentVisitor where TNode : TSqlFragment
    {
        public bool Found;
        public override void Visit(TSqlFragment node)
        {
            Found |= node is TNode;
            base.Visit(node);
        }
    }

    // ---------------------------------------------------------------- statements

    private string Statement(TSqlStatement s, string ind)
    {
        switch (s)
        {
            case BeginEndBlockStatement b:
                return Block(b.StatementList, ind);
            case PredicateSetStatement:
            case SetIdentityInsertStatement:
            case PrintStatement:
            case DeallocateCursorStatement:
            case SetTransactionIsolationLevelStatement:
            case SetOnOffStatement:
                return "";
            case DeclareVariableStatement d:
                return DeclareInit(d, ind);
            case SetVariableStatement sv:
                return SetVariable(sv, ind);
            case SelectStatement sel:
                return SelectStatementSql(sel, ind);
            case UpdateStatement up:
                lastWasSelect = false;
                return ind + Update(up.UpdateSpecification) + ";\n";
            case InsertStatement ins:
                lastWasSelect = false;
                return ind + Insert(ins.InsertSpecification) + ";\n";
            case DeleteStatement del:
                lastWasSelect = false;
                return ind + Delete(del.DeleteSpecification) + ";\n";
            case IfStatement i:
                return If(i, ind);
            case WhileStatement w:
                return While(w, ind);
            case BreakStatement:
                return ind + "LEAVE " + CurrentLoop() + ";\n";
            case ContinueStatement:
                return ind + "ITERATE " + CurrentLoop() + ";\n";
            case ReturnStatement r:
                return Return(r, ind);
            case BeginTransactionStatement:
                return ind + "START TRANSACTION;\n";
            case CommitTransactionStatement:
                return ind + "COMMIT;\n";
            case RollbackTransactionStatement:
                return ind + "ROLLBACK;\n";
            case ExecuteStatement e:
                lastWasSelect = false;
                return Execute(e, ind);
            case DropTableStatement dt:
                return DropTable(dt, ind);
            case CreateTableStatement ct:
                return ind + CreateTemp(ct.SchemaObjectName, ct.Definition) + ";\n";
            case DeclareTableVariableStatement tv:
                return ind + "DROP TEMPORARY TABLE IF EXISTS " + TempName(tv.Body.VariableName.Value) + ";\n"
                    + ind + CreateTemp(tv.Body.VariableName.Value, tv.Body.Definition) + ";\n";
            case TruncateTableStatement tt:
                return ind + "TRUNCATE TABLE " + TableName(tt.TableName) + ";\n";
            case RaiseErrorStatement re:
                return RaiseError(re, ind);
            case DeclareCursorStatement dc:
                cursors.Add("DECLARE " + Program.Q(dc.Name.Value) + " CURSOR FOR " + Query(dc.CursorDefinition.Select.QueryExpression, null).Sql + ";");
                return "";
            case OpenCursorStatement oc:
                return ind + "OPEN " + Program.Q(oc.Cursor.Name.Value) + ";\n";
            case CloseCursorStatement cc:
                return ind + "CLOSE " + Program.Q(cc.Cursor.Name.Value) + ";\n";
            case FetchCursorStatement fc:
                if (fc.FetchType != null && fc.FetchType.Orientation is not (FetchOrientation.Next or FetchOrientation.None))
                {
                    throw new UnsupportedException("FETCH " + fc.FetchType.Orientation);
                }
                return ind + "SET v__fetch_status = 0;\n" + ind + "FETCH " + Program.Q(fc.Cursor.Name.Value) + " INTO "
                    + string.Join(", ", fc.IntoVariables.Select(v => Var(v.Name).Name)) + ";\n";
            case TryCatchStatement tc:
                return ind + "BEGIN\n" + ind + "  DECLARE EXIT HANDLER FOR SQLEXCEPTION\n" + ind + "  BEGIN\n"
                    + Statements(tc.CatchStatements, ind + "    ") + ind + "  END;\n"
                    + Statements(tc.TryStatements, ind + "  ") + ind + "END;\n";
            default:
                throw new UnsupportedException("statement " + s.GetType().Name);
        }
    }

    private string Statements(StatementList list, string ind)
    {
        StringBuilder sb = new();
        foreach (TSqlStatement s in list.Statements)
        {
            sb.Append(Statement(s, ind));
        }
        return sb.ToString();
    }

    private string Block(StatementList list, string ind) => Statements(list, ind);

    /// <summary>A statement list that must not be empty (IF/WHILE bodies).</summary>
    private string Body(TSqlStatement s, string ind)
    {
        string sql = Statement(s, ind);
        return sql.Trim().Length == 0 ? ind + "DO 0;\n" : sql;
    }

    private string DeclareInit(DeclareVariableStatement d, string ind)
    {
        StringBuilder sb = new();
        foreach (DeclareVariableElement e in d.Declarations.Where(e => e.Value != null))
        {
            sb.Append(ind + "SET " + Var(e.VariableName.Value).Name + " = " + Expr(e.Value).Sql + ";\n");
        }
        return sb.ToString();
    }

    private string SetVariable(SetVariableStatement sv, string ind)
    {
        if (sv.CursorDefinition != null || sv.Expression == null)
        {
            throw new UnsupportedException("SET @var = CURSOR / special SET");
        }
        (string name, T type) = Var(sv.Variable.Name);
        return ind + "SET " + name + " = " + Assign(name, type, sv.AssignmentKind, sv.Expression) + ";\n";
    }

    /// <summary>The value for "@x op= expr".</summary>
    private string Assign(string name, T type, AssignmentKind kind, ScalarExpression value)
    {
        (string sql, T vt) = Expr(value);
        return kind switch
        {
            AssignmentKind.Equals => sql,
            AssignmentKind.AddEquals => type == T.String || vt == T.String ? "CONCAT(" + name + ", " + sql + ")" : name + " + " + sql,
            AssignmentKind.SubtractEquals => name + " - (" + sql + ")",
            AssignmentKind.MultiplyEquals => name + " * (" + sql + ")",
            AssignmentKind.DivideEquals => name + (IsInteger(type) && IsInteger(vt) ? " DIV (" : " / (") + sql + ")",
            AssignmentKind.ModEquals => name + " % (" + sql + ")",
            _ => throw new UnsupportedException("assignment " + kind),
        };
    }

    private string SelectStatementSql(SelectStatement sel, string ind)
    {
        if (sel.Into != null)
        {
            lastWasSelect = false;
            string name = sel.Into.BaseIdentifier.Value;
            if (!name.StartsWith('#'))
            {
                throw new UnsupportedException("SELECT INTO a real table");
            }
            (string q, Dictionary<string, T> cols) = QueryWithColumns(sel.QueryExpression);
            tempTables[name] = cols;
            return ind + "DROP TEMPORARY TABLE IF EXISTS " + TempName(name) + ";\n" + ind + "CREATE TEMPORARY TABLE " + TempName(name) + " AS " + q + ";\n";
        }
        if (sel.QueryExpression is QuerySpecification qs && qs.SelectElements.Any(e => e is SelectSetVariable))
        {
            lastWasSelect = true;
            return ind + SelectAssign(qs) + "\n";
        }
        lastWasSelect = true;
        return ind + Query(sel.QueryExpression, null).Sql + ";\n";
    }

    /// <summary>SELECT @a = x, @b = y [FROM ...]: SET without FROM, SELECT ... INTO ... LIMIT 1 with it.</summary>
    private string SelectAssign(QuerySpecification qs)
    {
        if (!qs.SelectElements.All(e => e is SelectSetVariable))
        {
            throw new UnsupportedException("SELECT mixing assignments and columns");
        }
        if (qs.FromClause == null)
        {
            List<string> sets = new();
            foreach (SelectSetVariable v in qs.SelectElements.Cast<SelectSetVariable>())
            {
                (string name, T type) = Var(v.Variable.Name);
                sets.Add(name + " = " + Assign(name, type, v.AssignmentKind, v.Expression));
            }
            if (qs.WhereClause != null)
            {
                throw new UnsupportedException("SELECT @x = ... WHERE without FROM");
            }
            return "SET " + string.Join(", ", sets) + ";";
        }
        PushScope(qs.FromClause);
        try
        {
            List<string> cols = new();
            List<string> targets = new();
            List<SelectSetVariable> sets = qs.SelectElements.Cast<SelectSetVariable>().ToList();
            List<string> aggregates = sets.Select(v =>
            {
                (string name, T type) = Var(v.Variable.Name);
                return Accumulation(name, type, v.Variable.Name, v.AssignmentKind, v.Expression);
            }).ToList();
            if (aggregates.All(a => a != null) && qs.TopRowFilter == null)
            {
                // Only accumulations: one aggregate SELECT each, over every row.
                return string.Join(" ", sets.Select((v, i) => "SELECT " + aggregates[i] + " INTO " + Var(v.Variable.Name).Name + Tail(qs) + ";"));
            }
            if (aggregates.Any(a => a != null))
            {
                Warnings.Add("SELECT mixes @v = @v + x with other assignments: evaluated on one row (fine when the WHERE picks one row)");
            }
            foreach (SelectSetVariable v in sets)
            {
                (string name, T type) = Var(v.Variable.Name);
                cols.Add(Assign(name, type, v.AssignmentKind, v.Expression));
                targets.Add(name);
            }
            if (qs.OrderByClause != null && qs.TopRowFilter == null)
            {
                Warnings.Add("SELECT @var = ... ORDER BY without TOP: T-SQL keeps the last row, this keeps the first");
            }
            string sql = "SELECT " + string.Join(", ", cols) + " INTO " + string.Join(", ", targets) + Tail(qs) + " LIMIT 1";
            // A SELECT INTO that finds no row raises NOT FOUND, which the cursor handler would take as the end of the cursor.
            return hasCursor ? "BEGIN DECLARE CONTINUE HANDLER FOR NOT FOUND BEGIN END; " + sql + "; END;" : sql + ";";
        }
        finally
        {
            scopes.Pop();
        }
    }

    private string If(IfStatement i, string ind)
    {
        StringBuilder sb = new();
        sb.Append(ind + "IF " + Bool(i.Predicate) + " THEN\n");
        sb.Append(Body(i.ThenStatement, ind + "  "));
        TSqlStatement e = i.ElseStatement;
        while (e is IfStatement ei)
        {
            sb.Append(ind + "ELSEIF " + Bool(ei.Predicate) + " THEN\n");
            sb.Append(Body(ei.ThenStatement, ind + "  "));
            e = ei.ElseStatement;
        }
        if (e != null)
        {
            sb.Append(ind + "ELSE\n");
            sb.Append(Body(e, ind + "  "));
        }
        sb.Append(ind + "END IF;\n");
        return sb.ToString();
    }

    private string While(WhileStatement w, string ind)
    {
        string label = "w" + (++loopCount);
        loops.Push(label);
        try
        {
            return ind + label + ": WHILE " + Bool(w.Predicate) + " DO\n" + Body(w.Statement, ind + "  ") + ind + "END WHILE " + label + ";\n";
        }
        finally
        {
            loops.Pop();
        }
    }

    private string CurrentLoop() => loops.Count > 0 ? loops.Peek() : throw new UnsupportedException("BREAK/CONTINUE outside a loop");

    private string Return(ReturnStatement r, string ind)
    {
        if (isFunction)
        {
            return ind + "RETURN " + Expr(r.Expression).Sql + ";\n";
        }
        return (r.Expression != null ? ind + "SET p_ReturnValue = " + Expr(r.Expression).Sql + ";\n" : "") + ind + "LEAVE proc;\n";
    }

    private string RaiseError(RaiseErrorStatement re, string ind)
    {
        if (re.SecondParameter is IntegerLiteral sev && int.Parse(sev.Value) < 11)
        {
            return "";
        }
        return ind + "SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = " + Expr(re.FirstParameter).Sql + ";\n";
    }

    private string DropTable(DropTableStatement dt, string ind)
    {
        StringBuilder sb = new();
        foreach (SchemaObjectName o in dt.Objects)
        {
            string name = o.BaseIdentifier.Value;
            if (!name.StartsWith('#'))
            {
                throw new UnsupportedException("DROP TABLE of a real table");
            }
            sb.Append(ind + "DROP TEMPORARY TABLE IF EXISTS " + TempName(name) + ";\n");
        }
        return sb.ToString();
    }

    private string CreateTemp(SchemaObjectName name, TableDefinition def) => CreateTemp(name.BaseIdentifier.Value, def);

    private string CreateTemp(string name, TableDefinition def)
    {
        if (!name.StartsWith('#') && !name.StartsWith('@'))
        {
            throw new UnsupportedException("CREATE TABLE of a real table");
        }
        List<string> cols = new();
        foreach (ColumnDefinition c in def.ColumnDefinitions)
        {
            bool notNull = c.Constraints.OfType<NullableConstraintDefinition>().Any(n => !n.Nullable);
            string col = Program.Q(c.ColumnIdentifier.Value) + " " + MapDataType(c.DataType) + (notNull ? " NOT NULL" : " NULL");
            if (c.IdentityOptions != null)
            {
                col += " AUTO_INCREMENT PRIMARY KEY";
            }
            else if (c.DefaultConstraint != null)
            {
                col += " DEFAULT " + Expr(c.DefaultConstraint.Expression).Sql;
            }
            cols.Add(col);
        }
        return "CREATE TEMPORARY TABLE " + TempName(name) + " (" + string.Join(", ", cols) + ")";
    }

    private static string TempName(string name) => Program.Q((name.StartsWith('@') ? "tv_" : "tmp_") + name.TrimStart('#', '@'));

    private string Execute(ExecuteStatement e, string ind)
    {
        ExecuteSpecification spec = e.ExecuteSpecification;
        if (spec.ExecutableEntity is not ExecutableProcedureReference pr || pr.ProcedureReference?.ProcedureReference == null)
        {
            throw new UnsupportedException("dynamic SQL (EXEC string / sp_executesql)");
        }
        SchemaObjectName pn = pr.ProcedureReference.ProcedureReference.Name;
        string targetDb = string.IsNullOrEmpty(pn.DatabaseIdentifier?.Value) ? db : pn.DatabaseIdentifier.Value;
        string proc = pn.BaseIdentifier.Value;
        if (!schema.Procedures.TryGetValue(targetDb, out var procs) || !procs.TryGetValue(proc, out List<Schema.Param> sig))
        {
            throw new UnsupportedException("EXEC of unknown procedure " + proc);
        }
        string[] args = new string[sig.Count];
        int position = 0;
        foreach (ExecuteParameter p in pr.Parameters)
        {
            int index = p.Variable != null
                ? sig.FindIndex(s => string.Equals(s.Name, p.Variable.Name.TrimStart('@'), StringComparison.OrdinalIgnoreCase))
                : position++;
            if (index < 0 || index >= sig.Count)
            {
                throw new UnsupportedException("EXEC " + proc + ": unknown parameter");
            }
            args[index] = Expr(p.ParameterValue).Sql;
        }
        for (int i = 0; i < sig.Count; i++)
        {
            args[i] ??= sig[i].Default != null ? Expr(sig[i].Default).Sql : "NULL";
        }
        needReturnVar = true;
        string ret = spec.Variable != null ? Var(spec.Variable.Name).Name : "v__rv";
        string target = targetDb.Equals(db, StringComparison.OrdinalIgnoreCase) ? Program.Q(proc) : Program.Q(targetDb) + "." + Program.Q(proc);
        return ind + "CALL " + target + "(" + string.Join(", ", args.Append(ret)) + ");\n";
    }

    private string Update(UpdateSpecification u)
    {
        List<AssignmentSetClause> varSets = u.SetClauses.OfType<AssignmentSetClause>().Where(a => a.Variable != null).ToList();
        if (varSets.Count > 0)
        {
            return UpdateWithVariables(u, varSets);
        }
        string topLimit = u.TopRowFilter != null ? " LIMIT " + Expr(u.TopRowFilter.Expression).Sql : "";
        if (u.FromClause == null)
        {
            PushScopeFromTarget(u.Target);
            try
            {
                return "UPDATE " + TableRef(u.Target) + " SET " + SetClauses(u.SetClauses, null) + Where(u.WhereClause) + topLimit;
            }
            finally
            {
                scopes.Pop();
            }
        }
        // T-SQL UPDATE t SET ... FROM <tables> WHERE: MariaDB's multi-table UPDATE <tables> SET ... WHERE.
        string targetName = TargetName(u.Target);
        bool targetInFrom = FromNames(u.FromClause).Contains(targetName, StringComparer.OrdinalIgnoreCase);
        PushScope(u.FromClause);
        if (!targetInFrom)
        {
            AddTargetToScope(u.Target);
        }
        try
        {
            string tables = (targetInFrom ? "" : TableRef(u.Target) + ", ") + FromTables(u.FromClause);
            if (topLimit != "")
            {
                throw new UnsupportedException("UPDATE TOP with FROM");
            }
            return "UPDATE " + tables + " SET " + SetClauses(u.SetClauses, QualifierOf(u.Target, u.FromClause)) + Where(u.WhereClause);
        }
        finally
        {
            scopes.Pop();
        }
    }

    /// <summary>
    /// UPDATE t SET Col = x, @v = OtherCol WHERE ...: MariaDB cannot assign variables in an UPDATE. The variables
    /// are read first with a SELECT over the same rows (@v = @v + expr accumulates over all of them), then the
    /// UPDATE runs with the column assignments.
    /// </summary>
    private string UpdateWithVariables(UpdateSpecification u, List<AssignmentSetClause> varSets)
    {
        if (varSets.Any(a => a.Column != null))
        {
            throw new UnsupportedException("UPDATE SET @var = column = value");
        }
        if (u.TopRowFilter != null)
        {
            throw new UnsupportedException("UPDATE TOP with variable assignments");
        }
        bool hasFrom = u.FromClause != null;
        if (hasFrom)
        {
            PushScope(u.FromClause);
            if (!FromNames(u.FromClause).Contains(TargetName(u.Target), StringComparer.OrdinalIgnoreCase))
            {
                AddTargetToScope(u.Target);
            }
        }
        else
        {
            PushScopeFromTarget(u.Target);
        }
        List<string> selects = new();
        try
        {
            string from = " FROM " + (hasFrom && FromNames(u.FromClause).Contains(TargetName(u.Target), StringComparer.OrdinalIgnoreCase)
                ? FromTables(u.FromClause)
                : TableRef(u.Target) + (hasFrom ? ", " + FromTables(u.FromClause) : "")) + Where(u.WhereClause);
            List<string> single = new(), singleVars = new();
            foreach (AssignmentSetClause a in varSets)
            {
                (string name, T type) = Var(a.Variable.Name);
                string agg = Accumulation(name, type, a.Variable.Name, a.AssignmentKind, a.NewValue);
                if (agg != null)
                {
                    selects.Add("SELECT " + agg + " INTO " + name + from);
                }
                else
                {
                    single.Add(Assign(name, type, a.AssignmentKind, a.NewValue));
                    singleVars.Add(name);
                }
            }
            if (single.Count > 0)
            {
                string sql = "SELECT " + string.Join(", ", single) + " INTO " + string.Join(", ", singleVars) + from + " LIMIT 1";
                selects.Insert(0, hasCursor ? "BEGIN DECLARE CONTINUE HANDLER FOR NOT FOUND BEGIN END; " + sql + "; END" : sql);
            }
        }
        finally
        {
            scopes.Pop();
        }
        List<SetClause> columnSets = u.SetClauses.Where(c => c is not AssignmentSetClause a || a.Variable == null).ToList();
        if (columnSets.Count == 0)
        {
            return string.Join(";\n", selects);
        }
        UpdateSpecification rest2 = new UpdateSpecification { Target = u.Target, FromClause = u.FromClause, WhereClause = u.WhereClause };
        foreach (SetClause c in columnSets)
        {
            rest2.SetClauses.Add(c);
        }
        return string.Join(";\n", selects) + ";\n" + Update(rest2);
    }

    /// <summary>
    /// "@v = @v + expr" (or "@v += expr") evaluated once per row: the aggregate that gives the same final value
    /// (GROUP_CONCAT for strings, SUM for numbers), or null when the assignment does not accumulate.
    /// </summary>
    private string Accumulation(string name, T type, string tsqlName, AssignmentKind kind, ScalarExpression value)
    {
        List<ScalarExpression> rest;
        if (kind == AssignmentKind.AddEquals)
        {
            rest = new();
            Flatten(value, rest);
        }
        else if (kind == AssignmentKind.Equals && value is BinaryExpression b && b.BinaryExpressionType == BinaryExpressionType.Add
            && FirstTerm(b) is VariableReference vr && vr.Name.Equals(tsqlName, StringComparison.OrdinalIgnoreCase))
        {
            rest = new();
            Flatten(b, rest);
            rest.RemoveAt(0);
        }
        else
        {
            return null;
        }
        var parts = rest.Select(Expr).ToList();
        bool str = type == T.String || parts.Any(r => r.Type == T.String);
        string perRow = str ? "CONCAT(" + string.Join(", ", parts.Select(r => r.Sql)) + ")" : string.Join(" + ", parts.Select(r => r.Sql));
        return str ? "CONCAT(" + name + ", IFNULL(GROUP_CONCAT(" + perRow + " SEPARATOR ''), ''))" : name + " + IFNULL(SUM(" + perRow + "), 0)";
    }

    private sealed class ColumnNames : TSqlFragmentVisitor
    {
        public readonly List<string> Names = new();
        public override void Visit(ColumnReferenceExpression node)
        {
            if (node.MultiPartIdentifier != null)
            {
                Names.Add(node.MultiPartIdentifier.Identifiers.Last().Value);
            }
        }
    }

    private static ScalarExpression FirstTerm(BinaryExpression b)
    {
        ScalarExpression e = b;
        while (e is BinaryExpression bb && bb.BinaryExpressionType == BinaryExpressionType.Add)
        {
            e = bb.FirstExpression;
        }
        return e;
    }

    private string SetClauses(IList<SetClause> clauses, string qualifier)
    {
        List<string> sets = new();
        HashSet<string> assigned = new(StringComparer.OrdinalIgnoreCase);
        foreach (SetClause c in clauses)
        {
            if (c is AssignmentSetClause ac && ac.Column != null)
            {
                ColumnNames names = new();
                ac.NewValue?.Accept(names);
                string reread = names.Names.FirstOrDefault(assigned.Contains);
                if (reread != null)
                {
                    Warnings.Add("UPDATE reads " + reread + " after assigning it: T-SQL sees the old value, MariaDB the new one: " + Short(ac));
                }
                assigned.Add(ac.Column.MultiPartIdentifier.Identifiers.Last().Value);
            }
            if (c is not AssignmentSetClause a || a.Column == null || a.Variable != null)
            {
                throw new UnsupportedException("UPDATE SET @var = ...");
            }
            IList<Identifier> ids = a.Column.MultiPartIdentifier.Identifiers;
            string col = ids.Count == 1 && qualifier != null ? qualifier + "." + Program.Q(ids[0].Value) : Column(a.Column).Sql;
            (string colSql, T colType) = Column(a.Column);
            sets.Add(col + " = " + Assign(colSql, colType, a.AssignmentKind, a.NewValue));
        }
        return string.Join(", ", sets);
    }

    private string Delete(DeleteSpecification d)
    {
        string topLimit = d.TopRowFilter != null ? " LIMIT " + Expr(d.TopRowFilter.Expression).Sql : "";
        if (d.FromClause == null)
        {
            PushScopeFromTarget(d.Target);
            try
            {
                return "DELETE FROM " + TableRef(d.Target) + Where(d.WhereClause) + topLimit;
            }
            finally
            {
                scopes.Pop();
            }
        }
        string targetName = TargetName(d.Target);
        bool targetInFrom = FromNames(d.FromClause).Contains(targetName, StringComparer.OrdinalIgnoreCase);
        PushScope(d.FromClause);
        if (!targetInFrom)
        {
            AddTargetToScope(d.Target);
        }
        try
        {
            if (topLimit != "")
            {
                throw new UnsupportedException("DELETE TOP with FROM");
            }
            string tables = (targetInFrom ? "" : TableRef(d.Target) + ", ") + FromTables(d.FromClause);
            return "DELETE " + QualifierOf(d.Target, d.FromClause) + " FROM " + tables + Where(d.WhereClause);
        }
        finally
        {
            scopes.Pop();
        }
    }

    private string Insert(InsertSpecification ins)
    {
        if (ins.InsertOption == InsertOption.Over)
        {
            throw new UnsupportedException("INSERT OVER");
        }
        string target = "INSERT INTO " + TableRef(ins.Target);
        if (ins.Columns.Count > 0)
        {
            target += " (" + string.Join(", ", ins.Columns.Select(c => Program.Q(c.MultiPartIdentifier.Identifiers.Last().Value))) + ")";
        }
        switch (ins.InsertSource)
        {
            case ValuesInsertSource v when v.IsDefaultValues:
                return target + " VALUES ()";
            case ValuesInsertSource v:
                scopes.Push(new());
                try
                {
                    return target + " VALUES " + string.Join(", ", v.RowValues.Select(r => "(" + string.Join(", ", r.ColumnValues.Select(c => Expr(c).Sql)) + ")"));
                }
                finally
                {
                    scopes.Pop();
                }
            case SelectInsertSource s:
                return target + " " + Query(s.Select, null).Sql;
            default:
                throw new UnsupportedException("INSERT ... EXEC");
        }
    }

    // ---------------------------------------------------------------- queries

    private (string Sql, T Type) Query(QueryExpression q, string _) => (QueryWithColumns(q).Sql, T.Unknown);

    private (string Sql, Dictionary<string, T> Columns) QueryWithColumns(QueryExpression q)
    {
        switch (q)
        {
            case QuerySpecification qs:
                return QuerySpec(qs);
            case QueryParenthesisExpression p:
                (string inner, var cols) = QueryWithColumns(p.QueryExpression);
                return ("(" + inner + ")", cols);
            case BinaryQueryExpression b:
                (string first, var firstCols) = QueryWithColumns(b.FirstQueryExpression);
                (string second, _) = QueryWithColumns(b.SecondQueryExpression);
                string op = b.BinaryQueryExpressionType switch
                {
                    BinaryQueryExpressionType.Union => "UNION",
                    BinaryQueryExpressionType.Except => "EXCEPT",
                    BinaryQueryExpressionType.Intersect => "INTERSECT",
                    _ => throw new UnsupportedException("query operator"),
                } + (b.All ? " ALL" : "");
                string sql = first + "\n" + op + "\n" + second + OrderBy(b.OrderByClause, null) + Offset(b.OffsetClause);
                return (sql, firstCols);
            default:
                throw new UnsupportedException("query " + q.GetType().Name);
        }
    }

    private (string Sql, Dictionary<string, T> Columns) QuerySpec(QuerySpecification qs)
    {
        PushScope(qs.FromClause);
        try
        {
            Dictionary<string, T> cols = new(StringComparer.OrdinalIgnoreCase);
            List<string> elements = new();
            foreach (SelectElement e in qs.SelectElements)
            {
                switch (e)
                {
                    case SelectStarExpression star:
                        elements.Add(star.Qualifier == null ? "*" : QualifierName(star.Qualifier.Identifiers.Last().Value) + ".*");
                        foreach (var scope in scopes.Peek())
                        {
                            if (star.Qualifier == null || scope.Alias.Equals(star.Qualifier.Identifiers.Last().Value, StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var kv in scope.Columns ?? new())
                                {
                                    cols.TryAdd(kv.Key, kv.Value);
                                }
                            }
                        }
                        break;
                    case SelectScalarExpression s:
                        string alias = s.ColumnName?.Value;
                        (string sql, T type) = s.Expression is IdentityFunctionCall
                            ? ("ROW_NUMBER() OVER (" + OrderBy(qs.OrderByClause, null).Trim() + ")", T.Int)
                            : Expr(s.Expression);
                        elements.Add(sql + (alias != null ? " AS " + Program.Q(alias) : ""));
                        string colName = alias ?? (s.Expression is ColumnReferenceExpression c ? c.MultiPartIdentifier.Identifiers.Last().Value : null);
                        if (colName != null)
                        {
                            cols[colName] = type;
                        }
                        break;
                    default:
                        throw new UnsupportedException("select element " + e.GetType().Name);
                }
            }
            string distinct = qs.UniqueRowFilter == UniqueRowFilter.Distinct ? "DISTINCT " : "";
            if (qs.TopRowFilter != null && (qs.TopRowFilter.Percent || qs.TopRowFilter.WithTies))
            {
                throw new UnsupportedException("TOP PERCENT / WITH TIES");
            }
            string sql2 = "SELECT " + distinct + string.Join(", ", elements) + Tail(qs);
            if (qs.TopRowFilter != null)
            {
                sql2 += " LIMIT " + Expr(qs.TopRowFilter.Expression).Sql;
            }
            return (sql2, cols);
        }
        finally
        {
            scopes.Pop();
        }
    }

    /// <summary>FROM ... WHERE ... GROUP BY ... HAVING ... ORDER BY ... (the scope must already be pushed).</summary>
    private string Tail(QuerySpecification qs)
    {
        string sql = "";
        if (qs.FromClause != null)
        {
            sql += " FROM " + FromTables(qs.FromClause);
        }
        sql += Where(qs.WhereClause);
        if (qs.GroupByClause != null)
        {
            sql += " GROUP BY " + string.Join(", ", qs.GroupByClause.GroupingSpecifications.Select(g => g is ExpressionGroupingSpecification eg
                ? Expr(eg.Expression).Sql : throw new UnsupportedException("GROUP BY " + g.GetType().Name)));
        }
        if (qs.HavingClause != null)
        {
            sql += " HAVING " + Bool(qs.HavingClause.SearchCondition);
        }
        sql += OrderBy(qs.OrderByClause, null);
        sql += Offset(qs.OffsetClause);
        return sql;
    }

    private string Where(WhereClause w)
    {
        if (w == null)
        {
            return "";
        }
        if (w.Cursor != null)
        {
            throw new UnsupportedException("WHERE CURRENT OF");
        }
        return " WHERE " + Bool(w.SearchCondition);
    }

    private string OrderBy(OrderByClause o, string _)
    {
        if (o == null)
        {
            return "";
        }
        return " ORDER BY " + string.Join(", ", o.OrderByElements.Select(e => Expr(e.Expression).Sql + (e.SortOrder == SortOrder.Descending ? " DESC" : "")));
    }

    private string Offset(OffsetClause o)
    {
        return o == null ? "" : " LIMIT " + Expr(o.FetchExpression).Sql + " OFFSET " + Expr(o.OffsetExpression).Sql;
    }

    private string FromTables(FromClause f) => string.Join(", ", f.TableReferences.Select(TableRef));

    private string TableRef(TableReference t)
    {
        switch (t)
        {
            case NamedTableReference n:
                return TableName(n.SchemaObject) + (n.Alias != null ? " AS " + Program.Q(n.Alias.Value) : "");
            case QualifiedJoin j:
                string kind = j.QualifiedJoinType switch
                {
                    QualifiedJoinType.Inner => "INNER JOIN",
                    QualifiedJoinType.LeftOuter => "LEFT JOIN",
                    QualifiedJoinType.RightOuter => "RIGHT JOIN",
                    _ => throw new UnsupportedException("FULL OUTER JOIN"),
                };
                return TableRef(j.FirstTableReference) + " " + kind + " " + TableRef(j.SecondTableReference) + " ON " + Bool(j.SearchCondition);
            case UnqualifiedJoin u when u.UnqualifiedJoinType == UnqualifiedJoinType.CrossJoin:
                return TableRef(u.FirstTableReference) + " CROSS JOIN " + TableRef(u.SecondTableReference);
            case JoinParenthesisTableReference p:
                return "(" + TableRef(p.Join) + ")";
            case QueryDerivedTable d:
                return "(" + QueryWithColumns(d.QueryExpression).Sql + ") AS " + Program.Q(d.Alias.Value);
            case VariableTableReference v:
                return TempName(v.Variable.Name) + (v.Alias != null ? " AS " + Program.Q(v.Alias.Value) : "");
            case SchemaObjectFunctionTableReference f when f.SchemaObject.BaseIdentifier.Value.Equals("split", StringComparison.OrdinalIgnoreCase):
                // dbo.Split(list, sep) -> JSON_TABLE over a JSON array built from the list (columns id, value).
                string list = Expr(f.Parameters[0]).Sql;
                string sep = f.Parameters.Count > 1 ? Expr(f.Parameters[1]).Sql : "','";
                return "JSON_TABLE(CONCAT('[\"', REPLACE(REPLACE(REPLACE(" + list + ", '\\\\', '\\\\\\\\'), '\"', '\\\\\"'), " + sep + ", '\",\"'), '\"]'), '$[*]' COLUMNS (`id` FOR ORDINALITY, `value` VARCHAR(50) PATH '$')) AS "
                    + Program.Q(f.Alias?.Value ?? "split");
            default:
                throw new UnsupportedException("table reference " + t.GetType().Name);
        }
    }

    private string TableName(SchemaObjectName n)
    {
        string name = n.BaseIdentifier.Value;
        if (name.StartsWith('#'))
        {
            return TempName(name);
        }
        string targetDb = n.DatabaseIdentifier?.Value;
        return !string.IsNullOrEmpty(targetDb) && !targetDb.Equals(db, StringComparison.OrdinalIgnoreCase)
            ? Program.Q(targetDb) + "." + Program.Q(name)
            : Program.Q(name);
    }

    private static string TargetName(TableReference t) => t switch
    {
        NamedTableReference n => n.SchemaObject.BaseIdentifier.Value,
        _ => throw new UnsupportedException("UPDATE/DELETE target " + t.GetType().Name),
    };

    /// <summary>How the target is referred to in a multi-table UPDATE/DELETE: its alias or table name.</summary>
    private string QualifierOf(TableReference t, FromClause from = null)
    {
        NamedTableReference n = (NamedTableReference)t;
        if (n.Alias != null)
        {
            return Program.Q(n.Alias.Value);
        }
        string name = n.SchemaObject.BaseIdentifier.Value;
        // UPDATE Consortia SET ... FROM Consortia a: in MariaDB the table is known by its alias.
        NamedTableReference inFrom = from == null ? null : NamedTables(from).FirstOrDefault(x => x.SchemaObject.BaseIdentifier.Value.Equals(name, StringComparison.OrdinalIgnoreCase));
        return inFrom?.Alias != null ? Program.Q(inFrom.Alias.Value) : QualifierName(name);
    }

    private static IEnumerable<NamedTableReference> NamedTables(FromClause f)
    {
        List<NamedTableReference> list = new();
        void Walk(TableReference t)
        {
            switch (t)
            {
                case NamedTableReference n:
                    list.Add(n);
                    break;
                case JoinTableReference j:
                    Walk(j.FirstTableReference);
                    Walk(j.SecondTableReference);
                    break;
                case JoinParenthesisTableReference p:
                    Walk(p.Join);
                    break;
            }
        }
        foreach (TableReference t in f.TableReferences)
        {
            Walk(t);
        }
        return list;
    }

    private static IEnumerable<string> FromNames(FromClause f)
    {
        List<string> names = new();
        void Walk(TableReference t)
        {
            switch (t)
            {
                case NamedTableReference n:
                    names.Add(n.SchemaObject.BaseIdentifier.Value);
                    if (n.Alias != null)
                    {
                        names.Add(n.Alias.Value);
                    }
                    break;
                case JoinTableReference j:
                    Walk(j.FirstTableReference);
                    Walk(j.SecondTableReference);
                    break;
                case JoinParenthesisTableReference p:
                    Walk(p.Join);
                    break;
            }
        }
        foreach (TableReference t in f.TableReferences)
        {
            Walk(t);
        }
        return names;
    }

    // ---------------------------------------------------------------- name scopes (for column types)

    private void PushScope(FromClause f)
    {
        List<(string, Dictionary<string, T>, string)> scope = new();
        if (f != null)
        {
            foreach (TableReference t in f.TableReferences)
            {
                AddToScope(scope, t);
            }
        }
        scopes.Push(scope);
    }

    private void PushScopeFromTarget(TableReference target)
    {
        List<(string, Dictionary<string, T>, string)> scope = new();
        AddToScope(scope, target);
        scopes.Push(scope);
    }

    private void AddTargetToScope(TableReference target) => AddToScope(scopes.Peek(), target);

    private void AddToScope(List<(string Alias, Dictionary<string, T> Columns, string Table)> scope, TableReference t)
    {
        switch (t)
        {
            case NamedTableReference n:
                string name = n.SchemaObject.BaseIdentifier.Value;
                Dictionary<string, T> cols = name.StartsWith('#')
                    ? tempTables.GetValueOrDefault(name)
                    : schema.FindTable(string.IsNullOrEmpty(n.SchemaObject.DatabaseIdentifier?.Value) ? db : n.SchemaObject.DatabaseIdentifier.Value, name);
                scope.Add((n.Alias?.Value ?? name, cols, name));
                break;
            case VariableTableReference v:
                scope.Add((v.Alias?.Value ?? v.Variable.Name, tempTables.GetValueOrDefault(v.Variable.Name), v.Variable.Name));
                break;
            case JoinTableReference j:
                AddToScope(scope, j.FirstTableReference);
                AddToScope(scope, j.SecondTableReference);
                break;
            case JoinParenthesisTableReference p:
                AddToScope(scope, p.Join);
                break;
            case QueryDerivedTable d:
                scope.Add((d.Alias.Value, QueryWithColumns(d.QueryExpression).Columns, null));
                break;
            case SchemaObjectFunctionTableReference f:
                scope.Add((f.Alias?.Value ?? "split", new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase) { ["id"] = T.Int, ["value"] = T.String }, null));
                break;
        }
    }

    private T TypeOfColumn(IList<Identifier> ids)
    {
        string col = ids.Last().Value;
        string qualifier = ids.Count >= 2 ? ids[^2].Value : null;
        foreach (var scope in scopes)
        {
            foreach (var entry in scope)
            {
                if (qualifier != null && !entry.Alias.Equals(qualifier, StringComparison.OrdinalIgnoreCase)
                    && !(entry.Table ?? "").Equals(qualifier, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (entry.Columns != null && entry.Columns.TryGetValue(col, out T type))
                {
                    return type;
                }
            }
        }
        return T.Unknown;
    }

    // ---------------------------------------------------------------- expressions

    private (string Name, T Type) Var(string tsqlName)
    {
        if (vars.TryGetValue(tsqlName, out var v))
        {
            return v;
        }
        throw new UnsupportedException("undeclared variable " + tsqlName);
    }

    private (string Sql, T Type) Column(ColumnReferenceExpression c)
    {
        if (c.ColumnType == ColumnType.Wildcard)
        {
            return ("*", T.Unknown);
        }
        IList<Identifier> ids = c.MultiPartIdentifier.Identifiers;
        // db.dbo.table.column / dbo.table.column / alias.column: keep the last two parts.
        List<string> parts = new();
        if (ids.Count >= 2)
        {
            parts.Add(QualifierName(ids[^2].Value));
        }
        parts.Add(Program.Q(ids[^1].Value));
        return (string.Join(".", parts), TypeOfColumn(ids));
    }

    private static string QualifierName(string name) => name.StartsWith('#') ? TempName(name) : Program.Q(name);

    public (string Sql, T Type) Expr(ScalarExpression e)
    {
        switch (e)
        {
            case null:
                return ("NULL", T.Null);
            case IntegerLiteral i:
                return (i.Value, long.TryParse(i.Value, out long n) && (n > int.MaxValue || n < int.MinValue) ? T.BigInt : T.Int);
            case NumericLiteral num:
                return (num.Value, T.Decimal);
            case RealLiteral r:
                return (r.Value, T.Float);
            case MoneyLiteral m:
                return (m.Value.TrimStart('$'), T.Decimal);
            case StringLiteral s:
                return (DataWriter.Str(s.Value), T.String);
            case BinaryLiteral b:
                string hex = b.Value[2..];
                return (hex.Length == 0 ? "''" : "X'" + hex + "'", T.Binary);
            case NullLiteral:
                return ("NULL", T.Null);
            case DefaultLiteral:
                return ("DEFAULT", T.Unknown);
            case IdentifierLiteral id:
                // date part names (DATEDIFF(ss, ...)) are parsed as identifier literals.
                return (id.Value, T.Unknown);
            case VariableReference v:
                return Var(v.Name);
            case GlobalVariableExpression g:
                return Global(g.Name);
            case ColumnReferenceExpression c:
                return Column(c);
            case ParenthesisExpression p:
                (string ps, T pt) = Expr(p.Expression);
                return ("(" + ps + ")", pt);
            case UnaryExpression u:
                (string us, T ut) = Expr(u.Expression);
                return (u.UnaryExpressionType switch
                {
                    UnaryExpressionType.Negative => "-" + us,
                    UnaryExpressionType.Positive => us,
                    _ => "~" + us,
                }, ut);
            case BinaryExpression b:
                return Binary(b);
            case FunctionCall f:
                return Function(f);
            case LeftFunctionCall l:
                return ("LEFT(" + string.Join(", ", l.Parameters.Select(x => Expr(x).Sql)) + ")", T.String);
            case RightFunctionCall r:
                return ("RIGHT(" + string.Join(", ", r.Parameters.Select(x => Expr(x).Sql)) + ")", T.String);
            case CastCall cast:
                return Cast(Expr(cast.Parameter), cast.DataType, null);
            case ConvertCall conv:
                return Cast(Expr(conv.Parameter), conv.DataType, conv.Style);
            case TryCastCall tc:
                return Cast(Expr(tc.Parameter), tc.DataType, null);
            case SearchedCaseExpression sc:
                {
                    StringBuilder sb = new("CASE");
                    T type = T.Unknown;
                    foreach (SearchedWhenClause w in sc.WhenClauses)
                    {
                        (string then, T tt) = Expr(w.ThenExpression);
                        type = Merge(type, tt);
                        sb.Append(" WHEN " + Bool(w.WhenExpression) + " THEN " + then);
                    }
                    if (sc.ElseExpression != null)
                    {
                        (string el, T et) = Expr(sc.ElseExpression);
                        type = Merge(type, et);
                        sb.Append(" ELSE " + el);
                    }
                    return (sb.Append(" END").ToString(), type);
                }
            case SimpleCaseExpression ce:
                {
                    StringBuilder sb = new("CASE " + Expr(ce.InputExpression).Sql);
                    T type = T.Unknown;
                    foreach (SimpleWhenClause w in ce.WhenClauses)
                    {
                        (string then, T tt) = Expr(w.ThenExpression);
                        type = Merge(type, tt);
                        sb.Append(" WHEN " + Expr(w.WhenExpression).Sql + " THEN " + then);
                    }
                    if (ce.ElseExpression != null)
                    {
                        (string el, T et) = Expr(ce.ElseExpression);
                        type = Merge(type, et);
                        sb.Append(" ELSE " + el);
                    }
                    return (sb.Append(" END").ToString(), type);
                }
            case CoalesceExpression co:
                {
                    var parts = co.Expressions.Select(Expr).ToList();
                    return ("COALESCE(" + string.Join(", ", parts.Select(p2 => p2.Sql)) + ")", parts.Select(p2 => p2.Type).Aggregate(T.Unknown, Merge));
                }
            case NullIfExpression ni:
                {
                    var a = Expr(ni.FirstExpression);
                    return ("NULLIF(" + a.Sql + ", " + Expr(ni.SecondExpression).Sql + ")", a.Type);
                }
            case IIfCall iif:
                {
                    var a = Expr(iif.ThenExpression);
                    var b2 = Expr(iif.ElseExpression);
                    return ("IF(" + Bool(iif.Predicate) + ", " + a.Sql + ", " + b2.Sql + ")", Merge(a.Type, b2.Type));
                }
            case ScalarSubquery sq:
                {
                    (string sql, var cols) = QueryWithColumns(sq.QueryExpression);
                    return ("(" + sql + ")", cols.Count == 1 ? cols.Values.First() : T.Unknown);
                }
            case ParameterlessCall pc:
                return pc.ParameterlessCallType switch
                {
                    ParameterlessCallType.CurrentTimestamp => ("NOW(3)", T.DateTime),
                    ParameterlessCallType.CurrentUser or ParameterlessCallType.SessionUser or ParameterlessCallType.SystemUser or ParameterlessCallType.User => ("CURRENT_USER()", T.String),
                    _ => throw new UnsupportedException("parameterless call " + pc.ParameterlessCallType),
                };
            default:
                throw new UnsupportedException("expression " + e.GetType().Name);
        }
    }

    private (string, T) Global(string name)
    {
        switch (name.ToUpperInvariant())
        {
            case "@@ERROR":
                // Errors end a MariaDB procedure (rolled back by the handler), so code that runs never saw one.
                return ("0", T.Int);
            case "@@IDENTITY":
                return ("LAST_INSERT_ID()", T.Int);
            case "@@ROWCOUNT":
                return (lastWasSelect ? "FOUND_ROWS()" : "ROW_COUNT()", T.Int);
            case "@@FETCH_STATUS":
                return ("v__fetch_status", T.Int);
            case "@@TRANCOUNT":
                return ("@@in_transaction", T.Int);
            case "@@SPID":
                return ("CONNECTION_ID()", T.Int);
            default:
                throw new UnsupportedException("global " + name);
        }
    }

    private (string, T) Binary(BinaryExpression b)
    {
        if (b.BinaryExpressionType == BinaryExpressionType.Add)
        {
            List<ScalarExpression> terms = new();
            Flatten(b, terms);
            var parts = terms.Select(Expr).ToList();
            if (parts.Any(p => p.Type == T.String))
            {
                if (parts.Any(p => p.Type == T.Unknown))
                {
                    Warnings.Add("+ with a string and an operand of unknown type treated as concatenation: " + Short(b));
                }
                return ("CONCAT(" + string.Join(", ", parts.Select(p => p.Sql)) + ")", T.String);
            }
            if (parts.Any(p => p.Type == T.Unknown) && parts.Count(p => p.Type == T.Unknown) == parts.Count)
            {
                Warnings.Add("+ with operands of unknown type treated as numeric: " + Short(b));
            }
            if (parts[0].Type == T.DateTime && parts.Count == 2)
            {
                return ("DATE_ADD(" + parts[0].Sql + ", INTERVAL " + parts[1].Sql + " DAY)", T.DateTime);
            }
            return (string.Join(" + ", parts.Select(p => p.Sql)), parts.Select(p => p.Type).Aggregate(T.Unknown, Merge));
        }
        var l = Expr(b.FirstExpression);
        var r = Expr(b.SecondExpression);
        switch (b.BinaryExpressionType)
        {
            case BinaryExpressionType.Subtract:
                if (l.Type == T.DateTime && IsNumeric(r.Type))
                {
                    return ("DATE_SUB(" + l.Sql + ", INTERVAL " + r.Sql + " DAY)", T.DateTime);
                }
                return (l.Sql + " - " + Wrap(b.SecondExpression, r.Sql), Merge(l.Type, r.Type));
            case BinaryExpressionType.Multiply:
                return (l.Sql + " * " + Wrap(b.SecondExpression, r.Sql), Merge(l.Type, r.Type));
            case BinaryExpressionType.Divide:
                if (IsInteger(l.Type) && IsInteger(r.Type))
                {
                    return (l.Sql + " DIV " + Wrap(b.SecondExpression, r.Sql), T.Int);
                }
                if (l.Type == T.Unknown || r.Type == T.Unknown)
                {
                    Warnings.Add("/ with an operand of unknown type: decimal division assumed: " + Short(b));
                }
                return (l.Sql + " / " + Wrap(b.SecondExpression, r.Sql), Merge(l.Type, r.Type));
            case BinaryExpressionType.Modulo:
                return (l.Sql + " % " + Wrap(b.SecondExpression, r.Sql), Merge(l.Type, r.Type));
            case BinaryExpressionType.BitwiseAnd:
                return (l.Sql + " & " + Wrap(b.SecondExpression, r.Sql), T.Int);
            case BinaryExpressionType.BitwiseOr:
                return (l.Sql + " | " + Wrap(b.SecondExpression, r.Sql), T.Int);
            case BinaryExpressionType.BitwiseXor:
                return (l.Sql + " ^ " + Wrap(b.SecondExpression, r.Sql), T.Int);
            default:
                throw new UnsupportedException("operator " + b.BinaryExpressionType);
        }
    }

    /// <summary>The right operand of - * / %: T-SQL's tree already holds the grouping; parenthesize nested operators.</summary>
    private static string Wrap(ScalarExpression e, string sql) => e is BinaryExpression ? "(" + sql + ")" : sql;

    private static void Flatten(ScalarExpression e, List<ScalarExpression> terms)
    {
        if (e is BinaryExpression b && b.BinaryExpressionType == BinaryExpressionType.Add)
        {
            Flatten(b.FirstExpression, terms);
            Flatten(b.SecondExpression, terms);
        }
        else
        {
            terms.Add(e);
        }
    }

    private static string Short(TSqlFragment f)
    {
        string text = string.Concat(Enumerable.Range(f.FirstTokenIndex, f.LastTokenIndex - f.FirstTokenIndex + 1).Select(i => f.ScriptTokenStream[i].Text));
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Length > 100 ? text[..100] + "..." : text;
    }

    private (string, T) Function(FunctionCall f)
    {
        string name = f.FunctionName.Value;
        if (f.CallTarget is MultiPartIdentifierCallTarget target)
        {
            IList<Identifier> ids = target.MultiPartIdentifier.Identifiers;
            string fnDb = ids.Count >= 2 ? ids[^2].Value : db;
            string args2 = string.Join(", ", f.Parameters.Select(p => Expr(p).Sql));
            T ret = schema.Functions.TryGetValue(fnDb, out var fns) && fns.TryGetValue(name, out T rt) ? rt : T.Unknown;
            string qualified = fnDb.Equals(db, StringComparison.OrdinalIgnoreCase) ? Program.Q(name) : Program.Q(fnDb) + "." + Program.Q(name);
            return (qualified + "(" + args2 + ")", ret);
        }
        List<(string Sql, T Type)> a = f.Parameters.Select(Expr).ToList();
        string over = f.OverClause != null ? " OVER (" + Over(f.OverClause) + ")" : "";
        string distinct = f.UniqueRowFilter == UniqueRowFilter.Distinct ? "DISTINCT " : "";
        string Args() => string.Join(", ", a.Select(x => x.Sql));
        switch (name.ToUpperInvariant())
        {
            case "ISNULL": return ("IFNULL(" + Args() + ")", Merge(a[0].Type, a[1].Type));
            case "GETDATE":
            case "SYSDATETIME": return ("NOW(3)", T.DateTime);
            case "GETUTCDATE": return ("UTC_TIMESTAMP(3)", T.DateTime);
            case "COUNT":
            case "COUNT_BIG": return ("COUNT(" + distinct + (f.Parameters.Count == 0 || f.Parameters[0] is ColumnReferenceExpression { ColumnType: ColumnType.Wildcard } ? "*" : Args()) + ")" + over, T.Int);
            case "SUM":
            case "MAX":
            case "MIN":
            case "AVG": return (name.ToUpperInvariant() + "(" + distinct + Args() + ")" + over, a[0].Type);
            case "ROW_NUMBER":
            case "RANK":
            case "DENSE_RANK": return (name.ToUpperInvariant() + "()" + over, T.Int);
            case "LEN": return ("CHAR_LENGTH(RTRIM(" + a[0].Sql + "))", T.Int);
            case "DATALENGTH": return ("LENGTH(" + a[0].Sql + ")", T.Int);
            case "CHARINDEX": return ("LOCATE(" + Args() + ")", T.Int);
            case "SUBSTRING": return ("SUBSTRING(" + Args() + ")", T.String);
            case "REPLACE":
            case "LOWER":
            case "UPPER":
            case "LTRIM":
            case "RTRIM":
            case "REVERSE":
            case "SPACE": return (name.ToUpperInvariant() + "(" + Args() + ")", T.String);
            case "REPLICATE": return ("REPEAT(" + Args() + ")", T.String);
            case "STUFF": return ("INSERT(" + Args() + ")", T.String);
            case "ABS":
            case "FLOOR":
            case "SIGN":
            case "POWER":
            case "SQRT":
            case "EXP":
            case "LOG10": return (name.ToUpperInvariant() + "(" + Args() + ")", a[0].Type == T.Unknown ? T.Float : a[0].Type);
            case "CEILING": return ("CEILING(" + Args() + ")", a[0].Type);
            case "RAND": return ("RAND(" + Args() + ")", T.Float);
            case "ROUND":
                if (a.Count == 3 && a[2].Sql != "0")
                {
                    return ("TRUNCATE(" + a[0].Sql + ", " + a[1].Sql + ")", a[0].Type);
                }
                return ("ROUND(" + a[0].Sql + ", " + a[1].Sql + ")", a[0].Type);
            case "NEWID": return ("UUID()", T.String);
            case "CHECKSUM": return ("CRC32(" + Args() + ")", T.Int);
            case "YEAR":
            case "MONTH":
            case "DAY": return (name.ToUpperInvariant() + "(" + Args() + ")", T.Int);
            case "DATEADD":
                string unit = DatePart(f.Parameters[0]);
                unit = unit == "WEEKDAY" ? "DAY" : unit == "MILLISECOND" ? throw new UnsupportedException("DATEADD ms") : unit;
                return ("DATE_ADD(" + a[2].Sql + ", INTERVAL " + a[1].Sql + " " + unit + ")", T.DateTime);
            case "DATEDIFF": return (DateDiff(DatePart(f.Parameters[0]), a[1].Sql, a[2].Sql), T.Int);
            case "DATEPART": return (DatePartOf(DatePart(f.Parameters[0]), a[1].Sql), T.Int);
            case "OBJECT_ID":
                if (f.Parameters[0] is StringLiteral s && s.Value.StartsWith("tempdb", StringComparison.OrdinalIgnoreCase))
                {
                    // Only used as "IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t": the DROP has IF EXISTS.
                    return ("1", T.Int);
                }
                return ("(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = " + a[0].Sql + ")", T.Int);
            case "COALESCE": return ("COALESCE(" + Args() + ")", a.Select(x => x.Type).Aggregate(T.Unknown, Merge));
            case "NCHAR":
            case "CHAR": return ("CHAR(" + a[0].Sql + " USING utf8mb4)", T.String);
            case "ASCII":
            case "UNICODE": return ("ORD(" + a[0].Sql + ")", T.Int);
            case "STR": return ("CAST(" + a[0].Sql + " AS CHAR)", T.String);
            default:
                throw new UnsupportedException("function " + name);
        }
    }

    private string Over(OverClause o)
    {
        string sql = "";
        if (o.Partitions.Count > 0)
        {
            sql += "PARTITION BY " + string.Join(", ", o.Partitions.Select(p => Expr(p).Sql));
        }
        if (o.OrderByClause != null)
        {
            sql += OrderBy(o.OrderByClause, null);
        }
        return sql.Trim();
    }

    /// <summary>DATEADD/DATEDIFF part names (dd, day, mi, ...) as a MariaDB interval unit.</summary>
    private static string DatePart(ScalarExpression part)
    {
        string p = part switch
        {
            ColumnReferenceExpression c => c.MultiPartIdentifier.Identifiers[0].Value,
            IdentifierLiteral i => i.Value,
            StringLiteral s => s.Value,
            _ => null,
        };
        return (p ?? "").ToLowerInvariant() switch
        {
            "year" or "yy" or "yyyy" => "YEAR",
            "quarter" or "qq" or "q" => "QUARTER",
            "month" or "mm" or "m" => "MONTH",
            "dayofyear" or "dy" or "y" or "day" or "dd" or "d" => "DAY",
            "week" or "wk" or "ww" => "WEEK",
            "weekday" or "dw" or "w" => "WEEKDAY",
            "hour" or "hh" => "HOUR",
            "minute" or "mi" or "n" => "MINUTE",
            "second" or "ss" or "s" => "SECOND",
            "millisecond" or "ms" => "MILLISECOND",
            _ => throw new UnsupportedException("date part " + p),
        };
    }

    /// <summary>T-SQL DATEDIFF counts boundaries crossed (DATEDIFF(day, '23:59', '00:01') = 1); keep that.</summary>
    private static string DateDiff(string unit, string a, string b)
    {
        return unit switch
        {
            "YEAR" => "(YEAR(" + b + ") - YEAR(" + a + "))",
            "QUARTER" => "((YEAR(" + b + ") * 4 + QUARTER(" + b + ")) - (YEAR(" + a + ") * 4 + QUARTER(" + a + ")))",
            "MONTH" => "((YEAR(" + b + ") * 12 + MONTH(" + b + ")) - (YEAR(" + a + ") * 12 + MONTH(" + a + ")))",
            "DAY" => "DATEDIFF(" + b + ", " + a + ")",
            "WEEK" => "(FLOOR(DATEDIFF(" + b + ", '1900-01-07') / 7) - FLOOR(DATEDIFF(" + a + ", '1900-01-07') / 7))",
            "HOUR" => "TIMESTAMPDIFF(HOUR, DATE_FORMAT(" + a + ", '%Y-%m-%d %H:00:00'), DATE_FORMAT(" + b + ", '%Y-%m-%d %H:00:00'))",
            "MINUTE" => "TIMESTAMPDIFF(MINUTE, DATE_FORMAT(" + a + ", '%Y-%m-%d %H:%i:00'), DATE_FORMAT(" + b + ", '%Y-%m-%d %H:%i:00'))",
            "SECOND" => "TIMESTAMPDIFF(SECOND, DATE_FORMAT(" + a + ", '%Y-%m-%d %H:%i:%s'), DATE_FORMAT(" + b + ", '%Y-%m-%d %H:%i:%s'))",
            "MILLISECOND" => "(TIMESTAMPDIFF(MICROSECOND, " + a + ", " + b + ") DIV 1000)",
            _ => throw new UnsupportedException("DATEDIFF " + unit),
        };
    }

    private static string DatePartOf(string unit, string d) => unit switch
    {
        "YEAR" => "YEAR(" + d + ")",
        "QUARTER" => "QUARTER(" + d + ")",
        "MONTH" => "MONTH(" + d + ")",
        "DAY" => "DAY(" + d + ")",
        "WEEKDAY" => "DAYOFWEEK(" + d + ")",
        "HOUR" => "HOUR(" + d + ")",
        "MINUTE" => "MINUTE(" + d + ")",
        "SECOND" => "SECOND(" + d + ")",
        "MILLISECOND" => "(MICROSECOND(" + d + ") DIV 1000)",
        _ => throw new UnsupportedException("DATEPART " + unit),
    };

    private (string, T) Cast((string Sql, T Type) value, DataTypeReference type, ScalarExpression style)
    {
        T target = TypeOf(type);
        if (type is SqlDataTypeReference st && style != null && target == T.String && value.Type is T.DateTime or T.Unknown)
        {
            string format = (style as IntegerLiteral)?.Value switch
            {
                "120" or "20" => "%Y-%m-%d %H:%i:%s",
                "121" or "21" => "%Y-%m-%d %H:%i:%s.%f",
                "23" => "%Y-%m-%d",
                "112" => "%Y%m%d",
                "108" or "8" => "%H:%i:%s",
                "111" => "%Y/%m/%d",
                "102" => "%Y.%m.%d",
                "101" => "%m/%d/%Y",
                "103" => "%d/%m/%Y",
                _ => throw new UnsupportedException("CONVERT style " + Short(style)),
            };
            string formatted = "DATE_FORMAT(" + value.Sql + ", '" + format + "')";
            // CONVERT(varchar(10), date, 120) keeps the first 10 characters.
            if (st.Parameters.Count == 1 && st.Parameters[0] is Literal len && int.TryParse(len.Value, out int n))
            {
                formatted = "LEFT(" + formatted + ", " + n + ")";
            }
            return (formatted, T.String);
        }
        string sql = target switch
        {
            T.Int or T.BigInt => "CAST(" + value.Sql + " AS SIGNED)",
            T.Bit => "(" + value.Sql + " <> 0)",
            T.Float => "CAST(" + value.Sql + " AS DOUBLE)",
            T.Decimal => "CAST(" + value.Sql + " AS " + MapDataType(type) + ")",
            T.DateTime => "CAST(" + value.Sql + " AS DATETIME(3))",
            T.String => "CAST(" + value.Sql + " AS CHAR" + CharLength(type) + ")",
            T.Binary => "CAST(" + value.Sql + " AS BINARY)",
            _ => throw new UnsupportedException("CAST to " + Short(type)),
        };
        return (sql, target);
    }

    private static string CharLength(DataTypeReference type)
    {
        return type is SqlDataTypeReference st && st.Parameters.Count == 1 && st.Parameters[0] is IntegerLiteral len ? "(" + len.Value + ")" : "";
    }

    // ---------------------------------------------------------------- conditions

    public string Bool(BooleanExpression b)
    {
        switch (b)
        {
            case BooleanComparisonExpression c:
                string op = c.ComparisonType switch
                {
                    BooleanComparisonType.Equals => "=",
                    BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation => "<>",
                    BooleanComparisonType.GreaterThan => ">",
                    BooleanComparisonType.LessThan => "<",
                    BooleanComparisonType.GreaterThanOrEqualTo or BooleanComparisonType.NotLessThan => ">=",
                    BooleanComparisonType.LessThanOrEqualTo or BooleanComparisonType.NotGreaterThan => "<=",
                    _ => throw new UnsupportedException("comparison " + c.ComparisonType),
                };
                return Expr(c.FirstExpression).Sql + " " + op + " " + Expr(c.SecondExpression).Sql;
            case BooleanBinaryExpression bb:
                return Bool(bb.FirstExpression) + (bb.BinaryExpressionType == BooleanBinaryExpressionType.And ? " AND " : " OR ") + Bool(bb.SecondExpression);
            case BooleanNotExpression n:
                return "NOT " + Bool(n.Expression);
            case BooleanParenthesisExpression p:
                return "(" + Bool(p.Expression) + ")";
            case BooleanIsNullExpression isNull:
                return Expr(isNull.Expression).Sql + (isNull.IsNot ? " IS NOT NULL" : " IS NULL");
            case InPredicate inp:
                string list = inp.Subquery != null ? Query(inp.Subquery.QueryExpression, null).Sql : string.Join(", ", inp.Values.Select(v => Expr(v).Sql));
                return Expr(inp.Expression).Sql + (inp.NotDefined ? " NOT IN (" : " IN (") + list + ")";
            case LikePredicate like:
                string pattern = Expr(like.SecondExpression).Sql;
                if (like.SecondExpression is StringLiteral sl && sl.Value.Contains('['))
                {
                    throw new UnsupportedException("LIKE with [ ] pattern");
                }
                return Expr(like.FirstExpression).Sql + (like.NotDefined ? " NOT LIKE " : " LIKE ") + pattern
                    + (like.EscapeExpression != null ? " ESCAPE " + Expr(like.EscapeExpression).Sql : "");
            case ExistsPredicate ex:
                return "EXISTS (" + Query(ex.Subquery.QueryExpression, null).Sql + ")";
            case BooleanTernaryExpression t:
                return Expr(t.FirstExpression).Sql + (t.TernaryExpressionType == BooleanTernaryExpressionType.NotBetween ? " NOT BETWEEN " : " BETWEEN ")
                    + Expr(t.SecondExpression).Sql + " AND " + Expr(t.ThirdExpression).Sql;
            default:
                throw new UnsupportedException("condition " + b.GetType().Name);
        }
    }

    // ---------------------------------------------------------------- types

    private static T Merge(T a, T b)
    {
        if (a == b || b == T.Null || b == T.Unknown)
        {
            return a == T.Null ? b : a;
        }
        if (a == T.Null || a == T.Unknown)
        {
            return b;
        }
        if (a == T.String || b == T.String)
        {
            return T.String;
        }
        if (a == T.DateTime || b == T.DateTime)
        {
            return T.DateTime;
        }
        if (a == T.Float || b == T.Float)
        {
            return T.Float;
        }
        if (a == T.Decimal || b == T.Decimal)
        {
            return T.Decimal;
        }
        if (a == T.BigInt || b == T.BigInt)
        {
            return T.BigInt;
        }
        return T.Int;
    }

    private static bool IsInteger(T t) => t is T.Int or T.BigInt or T.Bit;

    private static bool IsNumeric(T t) => t is T.Int or T.BigInt or T.Bit or T.Float or T.Decimal;

    public static T TypeOf(string sqlServerType) => sqlServerType.ToLowerInvariant() switch
    {
        "int" or "smallint" or "tinyint" => T.Int,
        "bigint" => T.BigInt,
        "bit" => T.Bit,
        "float" or "real" => T.Float,
        "decimal" or "numeric" or "money" or "smallmoney" => T.Decimal,
        "nvarchar" or "varchar" or "nchar" or "char" or "ntext" or "text" or "xml" or "uniqueidentifier" => T.String,
        "datetime" or "datetime2" or "smalldatetime" or "date" or "time" => T.DateTime,
        "varbinary" or "binary" or "image" or "timestamp" or "rowversion" => T.Binary,
        _ => T.Unknown,
    };

    public static T TypeOf(DataTypeReference type)
    {
        return type is SqlDataTypeReference st ? TypeOf(st.SqlDataTypeOption.ToString()) : T.Unknown;
    }

    /// <summary>A T-SQL type in a declaration or parameter, as a MariaDB type.</summary>
    public static string MapDataType(DataTypeReference type)
    {
        if (type is not SqlDataTypeReference st)
        {
            throw new UnsupportedException("type " + type.GetType().Name + " " + (type as UserDataTypeReference)?.Name?.BaseIdentifier?.Value);
        }
        IList<Literal> p = st.Parameters;
        bool max = p.Count == 1 && p[0] is MaxLiteral;
        int Len(int fallback) => p.Count >= 1 && int.TryParse(p[0].Value, out int n) ? n : fallback;
        return st.SqlDataTypeOption switch
        {
            SqlDataTypeOption.Int => "INT",
            SqlDataTypeOption.BigInt => "BIGINT",
            SqlDataTypeOption.SmallInt => "SMALLINT",
            SqlDataTypeOption.TinyInt => "TINYINT UNSIGNED",
            SqlDataTypeOption.Bit => "TINYINT(1)",
            SqlDataTypeOption.Float => "DOUBLE",
            SqlDataTypeOption.Real => "FLOAT",
            SqlDataTypeOption.Decimal or SqlDataTypeOption.Numeric => "DECIMAL(" + Len(18) + "," + (p.Count >= 2 ? p[1].Value : "0") + ")",
            SqlDataTypeOption.Money => "DECIMAL(19,4)",
            SqlDataTypeOption.SmallMoney => "DECIMAL(10,4)",
            SqlDataTypeOption.DateTime or SqlDataTypeOption.DateTime2 or SqlDataTypeOption.SmallDateTime => "DATETIME(3)",
            SqlDataTypeOption.Date => "DATE",
            SqlDataTypeOption.Time => "TIME(3)",
            SqlDataTypeOption.UniqueIdentifier => "CHAR(36)",
            SqlDataTypeOption.NVarChar or SqlDataTypeOption.VarChar => max ? "LONGTEXT" : "VARCHAR(" + Len(1) + ")",
            SqlDataTypeOption.NChar or SqlDataTypeOption.Char => "CHAR(" + Len(1) + ")",
            SqlDataTypeOption.NText or SqlDataTypeOption.Text => "LONGTEXT",
            SqlDataTypeOption.VarBinary or SqlDataTypeOption.Binary => max ? "LONGBLOB" : "VARBINARY(" + Len(1) + ")",
            SqlDataTypeOption.Image => "LONGBLOB",
            _ => throw new UnsupportedException("type " + st.SqlDataTypeOption),
        };
    }
}
