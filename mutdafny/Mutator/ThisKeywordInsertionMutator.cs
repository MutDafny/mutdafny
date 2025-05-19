using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class ThisKeywordInsertionMutator(string mutationTargetPos, ErrorReporter reporter): ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        
        var thisExpr = new ThisExpr(originalExpr.Origin);
        var nameValue = originalExpr is NameSegment nSegExpr ? nSegExpr.Name : "";
        var fieldName = new Name(originalExpr.Origin, nameValue);
        return new ExprDotName(originalExpr.Origin, thisExpr, fieldName, null);
    }

    private bool IsTarget(NameSegment nSegExpr) {
        return nSegExpr.Center.pos == int.Parse(MutationTargetPos);
    }

    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsTarget(nSegExpr))
            TargetExpression = nSegExpr;
    }
}