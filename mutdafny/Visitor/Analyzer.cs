using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class Analyzer(string MutationTargetURI, ErrorReporter reporter) : Visitor("-1", reporter)
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
    public override void Find(ModuleDefinition module) {
        // only visit modules that may contain the mutation target
        if ((ProgramAnalyzer.FirstCall && module.EndToken.pos == 0) || // default module
            MutationTargetURI == "" ||
            (module.Origin.Uri != null && module.Origin.Uri.LocalPath != null &&
            module.Origin.Uri.LocalPath.Contains(MutationTargetURI)))
        {
            base.Find(module);
        }
    }
    
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
        if (MutationTargetURI != "" && !methodOrFunc.Origin.Uri.LocalPath.Contains(MutationTargetURI))
            return;
            
        Methods.Add((methodOrFunc.Name,
            $"{methodOrFunc.StartToken.pos}", $"{methodOrFunc.EndToken.pos}",
            $"{methodOrFunc.Req.Count}", $"{methodOrFunc.Ens.Count}"
        ));
    }
}