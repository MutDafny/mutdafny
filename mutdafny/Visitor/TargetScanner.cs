using Microsoft.Dafny;

namespace MutDafny.Visitor;

public abstract class TargetScanner(string MutationTargetURI, List<string> operatorsInUse, ErrorReporter reporter) : Visitor("-1", reporter)
{
    protected List<(string, string, string)> Targets { get; } = [];
    protected bool IsParentSpec;
    protected bool IsFirstVisit = true;
    
    protected bool ShouldImplement(string op) {
        if (op != "THI" && op != "THD" && op != "AMR" && op != "MMR") {
            return IsFirstVisit && !IsParentSpec && (operatorsInUse.Count == 0 || operatorsInUse.Contains(op));
        }
        return !IsFirstVisit && !IsParentSpec && (operatorsInUse.Count == 0 || operatorsInUse.Contains(op));
    }
    
    public void ExportTargets() {
        using StreamWriter sw = File.AppendText("targets.csv");
        foreach (var target in Targets) {
            var line = target.Item1 + "," + target.Item2 + "," + target.Item3;
            sw.WriteLine(line);
        }
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