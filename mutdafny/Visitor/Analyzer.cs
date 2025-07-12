using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class Analyzer(ErrorReporter reporter): Visitor("-1", reporter)
{
    // method name, start position, end position, number of preconditions, number of postconditions
    private List<(string, string, string, string, string)> Methods { get; } = [];
    
    public void ExportData() {
        using StreamWriter sw = File.AppendText("methods.csv");
        foreach (var method in Methods) {
            var line = method.Item1 + "," + 
                       method.Item2 + "," + method.Item3 + "," + 
                       method.Item4 + "," + method.Item5;
            sw.WriteLine(line);
        }
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member.IsGhost) continue; // only searches for mutation targets in non-ghost constructs
            if (member is MethodOrFunction mf) {
                HandleMethod(mf);
            }
        }
        base.HandleMemberDecls(decl);
    }

    private void HandleMethod(MethodOrFunction methodOrFunc) {
        Methods.Add((methodOrFunc.Name,
            $"{methodOrFunc.StartToken.pos}", $"{methodOrFunc.EndToken.pos}",
            $"{methodOrFunc.Req.Count}", $"{methodOrFunc.Ens.Count}"
        ));
    }
}