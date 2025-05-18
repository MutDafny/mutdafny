using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class DatatypeCtorReplacementMutator(string mutationTargetPos, string ctorName, ErrorReporter reporter): Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(Token token) {
        return token.pos == int.Parse(MutationTargetPos);
    }

    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitExpression(NameSegment nSegExpr) {
        if (!IsTarget(nSegExpr.Center))
            return;
        TargetExpression = nSegExpr;
        nSegExpr.Name = ctorName;
    }

    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (suffixExpr is not ApplySuffix appSufExpr ||
            !IsTarget(appSufExpr.Center)) {
            base.VisitExpression(suffixExpr);
            return;
        }

        TargetExpression = suffixExpr;
        if (appSufExpr.Lhs is not NameSegment nSegExpr) return;
        nSegExpr.Name = ctorName;
    }
}