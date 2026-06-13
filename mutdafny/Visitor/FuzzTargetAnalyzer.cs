using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class FuzzTargetAnalyzer(string programName, string mutationTargetPos, string mutationOperator, string? mutationArg, ErrorReporter reporter) 
    : Visitor(mutationTargetPos, reporter)
{
    // module, class, method, argument types
    private List<(string, string, string, List<string>)> FuzzInfo { get; } = [];
    
    public void ExportData() {
        var filename = Path.GetFileNameWithoutExtension(programName);
        filename += mutationArg != "" ? 
            $"__{mutationTargetPos}_{mutationOperator}_{mutationArg}__fuzz-info.csv" : 
            $"__{mutationTargetPos}_{mutationOperator}__fuzz-info.csv"; 
        
        using StreamWriter sw = File.AppendText(filename);
        foreach (var methodInfo in FuzzInfo) {
            var line = methodInfo.Item1 + "," + methodInfo.Item2 + "," + methodInfo.Item3;
            foreach (var type in methodInfo.Item4)
                line += "," + type;
            sw.WriteLine(line);
        }
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void HandleMethod(Method method) {
        GetFuzzInfo(method);
    }

    protected override void HandleFunction(Function function) {
        GetFuzzInfo(function);
    }

    private void GetFuzzInfo(MethodOrFunction method) {
        var locationElems = method.FullSanitizedName.Split('.');
        if (locationElems.Length < 3) return;
        var moduleName = locationElems[0];
        var className = locationElems[1];
        var methodName = locationElems[2];
        
        var types = new List<string>();
        foreach (var input in method.Ins)
            types.Add(input.Type.ToString());
        
        FuzzInfo.Add((moduleName, className, methodName, types));
    }
    
    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        return MutationTargetPos == "-" || base.IsWorthVisiting(tokenStartPos, tokenEndPos);
    }
}