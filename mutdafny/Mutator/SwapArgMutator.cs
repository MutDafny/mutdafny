using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class SwapArgMutator(string mutationTargetPos, string newArgPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private int _targetBindingPos = -1;
    private int _replacementBindingPos = -1;
    
    private void Mutate(List<ActualBinding> bindings) {
        var targetBinding = bindings[_targetBindingPos];
        var replacementBinding = bindings[_replacementBindingPos];
        
        var targetBindingClone = new ActualBinding(targetBinding.Origin, targetBinding.Actual);
        bindings[_targetBindingPos].Actual = replacementBinding.Actual;
        bindings[_replacementBindingPos].Actual = targetBindingClone.Actual;
    }
    
    private bool IsTarget(Expression actualBinding) {
        return actualBinding.Center.pos == int.Parse(MutationTargetPos);
    }
    
    private bool IsReplacement(Expression actualBinding) {
        return actualBinding.Center.pos == int.Parse(newArgPos);
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void HandleActualBindings(ActualBindings bindings) {
        foreach (var (binding, i) in bindings.ArgumentBindings.Select((binding, i) => (binding, i))) {
            if (IsTarget(binding.Actual)) {
                TargetExpression = binding.Actual;
                _targetBindingPos = i;
            }
            if (IsReplacement(binding.Actual))
                _replacementBindingPos = i;
            if (_targetBindingPos != -1 && _replacementBindingPos != -1) {
                Mutate(bindings.ArgumentBindings);
                return;
            }
            
            if (!IsWorthVisiting(binding.Actual.StartToken.pos, binding.Actual.EndToken.pos))
                continue;
            HandleExpression(binding.Actual);
        }
    }
}