using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class PolymorphicRefReplacementMutator(string mutationTargetPos, string var, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(NameSegment nSegExpr) {
        return nSegExpr.Center.pos == int.Parse(MutationTargetPos);
    }

    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsTarget(nSegExpr)) {
            TargetExpression = nSegExpr;
            nSegExpr.Name = var;
            return;
        }
        base.VisitExpression(nSegExpr);
    }
}