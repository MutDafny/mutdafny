using Microsoft.Dafny;

namespace MutDafny.Visitor;

public abstract class TargetScanner(List<string> operatorsInUse, ErrorReporter reporter) : Visitor("-1", reporter)
{
    protected List<(string, string, string)> Targets { get; } = [];
    
    protected bool ShouldImplement(string op) {
        return operatorsInUse.Count == 0 || operatorsInUse.Contains(op);
    }
    
    public void ExportTargets() {
        using StreamWriter sw = File.AppendText("targets.csv");
        foreach (var target in Targets) {
            var line = target.Item1 + "," + target.Item2 + "," + target.Item3;
            sw.WriteLine(line);
        }
    }
}