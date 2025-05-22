using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MethodBodyReplacementMutator(string mutationTargetPos, string replacementPos, ErrorReporter reporter): Mutator(mutationTargetPos, reporter)
{
    private Method? _targetMethod;
    private Method? _replacementMethod;
    
    private bool IsTarget(Method method) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
                
        return method.StartToken.pos == startPosition && method.EndToken.pos == endPosition;
    }
    
    private bool IsReplacement(Method method) {
        var positions = replacementPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
                
        return method.StartToken.pos == startPosition && method.EndToken.pos == endPosition;
    }

    private void ReplaceMethodsBodies() {
        if (_targetMethod == null || _replacementMethod == null || 
            _targetMethod.Body == null || _replacementMethod.Body == null) 
            return;

        var cloner = new Cloner();
        var targetMethodBody = _targetMethod.Body.Clone(cloner);
        _targetMethod.Body = _replacementMethod.Body;
        _replacementMethod.Body = targetMethodBody;
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void HandleMethod(Method method) {
        if (IsTarget(method)) {
            _targetMethod = method;
            if (_replacementMethod != null) {
                ReplaceMethodsBodies();
                TargetStatement = method.Body;
            }
        }

        if (IsReplacement(method)) {
            _replacementMethod = method;
            if (_targetMethod != null) {
                ReplaceMethodsBodies();
                TargetStatement = _targetMethod.Body;
            }
        }
        
        base.HandleMethod(method);
    }

    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        return true;
    }
}