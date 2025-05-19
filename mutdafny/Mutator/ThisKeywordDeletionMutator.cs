using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

public class ThisKeywordDeletionMutator(string mutationTargetPos, ErrorReporter reporter): ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override Expression CreateMutatedExpression(Expression originalExpr)
    {
        TargetExpression = null;
        var nameValue = originalExpr is ExprDotName exprDName ? exprDName.SuffixName : "";
        return new NameSegment(originalExpr.Origin, nameValue, null);
    }
    
    private bool IsTarget(ExprDotName exprDName) {
        return exprDName.Center.pos == int.Parse(MutationTargetPos);
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (suffixExpr is ExprDotName exprDName && IsTarget(exprDName)) {
            TargetExpression = suffixExpr;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
}