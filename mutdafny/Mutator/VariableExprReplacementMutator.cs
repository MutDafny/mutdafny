using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class VariableExprReplacementMutator(string mutationTargetPos, string var, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(NameSegment nSegExpr) {
        return nSegExpr.Center.pos == int.Parse(MutationTargetPos) &&
               !AlreadyMutated(nSegExpr) && !ContainsMutatedChildren(nSegExpr);
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsTarget(nSegExpr)) {
            MutantGenerator.NumMutations++;
            MutantGenerator.MutatedNodes.Add(nSegExpr);
            TargetExpression = nSegExpr;
            nSegExpr.Name = var;
            return;
        }
        base.VisitExpression(nSegExpr);
    }
}