using Microsoft.Dafny;

namespace MutDafny.Visitor;

public abstract class TargetScanner(string MutationTargetURI, List<string> operatorsInUse, ErrorReporter reporter) : Visitor("-1", reporter)
{
    protected List<(string, string, string)> Targets { get; } = ImportPreviousTargets();
    protected bool IsParentSpec;
    protected bool IsFirstVisit = true;
    protected static List<Statement> OriginalStmts { get; } = [];
    protected static List<ConstantField> OriginalFields { get; } = [];
    
    protected bool ShouldImplement(string op) {
        if (op != "THI" && op != "THD" && op != "AMR" && op != "MMR") {
            return IsFirstVisit && !IsParentSpec && (operatorsInUse.Count == 0 || operatorsInUse.Contains(op));
        }
        return !IsFirstVisit && !IsParentSpec && (operatorsInUse.Count == 0 || operatorsInUse.Contains(op));
    }
    
    private static List<(string, string, string)> ImportPreviousTargets() {
        if (!File.Exists("targets.csv"))
            return [];
        
        var targets = new List<(string, string, string)>();
        var lines = File.ReadAllLines("targets.csv");
        foreach (var line in lines) {
            var components = line.Split(',');
            if (components.Length < 2)
                continue;
            
            var mutationPos = components[0];
            var mutationOp = components[1];
            var mutationArg = components.Length > 2 ? components[2] : "";
                
            targets.Add((mutationPos, mutationOp, mutationArg));
        }
        return targets;
    }

    protected void AddTarget((string, string, string) target) {
        if (!Targets.Contains(target))
            Targets.Add(target);
    }
    
    public void ExportTargets() {
        using StreamWriter sw = File.CreateText("targets.csv");
        foreach (var target in Targets) {
            var line = target.Item1 + "," + target.Item2 + "," + target.Item3;
            sw.WriteLine(line);
        }
    }

    protected bool IsStatementOriginal(Statement stmt) {
        foreach (var originalStmt in OriginalStmts) {
            if (stmt.GetType() != originalStmt.GetType())
                continue;
            if (stmt.StartToken.pos == originalStmt.StartToken.pos &&
                stmt.EndToken.pos == originalStmt.EndToken.pos)
                return true;
        }
        return false;
    }
    
    protected bool IsFieldOriginal(ConstantField cf) {
        foreach (var originalField in OriginalFields) {
            if (cf.StartToken.pos == originalField.StartToken.pos &&
                cf.EndToken.pos == originalField.EndToken.pos)
                return true;
        }
        return false;
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    public override void Find(ModuleDefinition module) {
        // only visit modules that may contain the mutation target
        if ((MutationTargetScanner.FirstCall && module.EndToken.pos == 0) || // default module
            MutationTargetURI == "" ||
            (module.Origin.Uri != null && module.Origin.Uri.LocalPath != null &&
             module.Origin.Uri.LocalPath.Contains(MutationTargetURI)))
        {
            base.Find(module);
        }
    }
}