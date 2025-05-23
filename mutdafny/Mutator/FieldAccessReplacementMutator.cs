using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class FieldAccessReplacementMutator(string mutationTargetPos, string field, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private bool IsTarget(ExprDotName exprDName) {
        return exprDName.Center.pos == int.Parse(MutationTargetPos);
    }

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        if (originalExpr is not ExprDotName exprDName) 
            return originalExpr;

        var newName = new Name(originalExpr.Origin, field);
        return new ExprDotName(originalExpr.Origin, exprDName.Lhs, newName, exprDName.OptTypeArguments);
    }

    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (suffixExpr is ExprDotName exprDName && IsTarget(exprDName)) {
            TargetExpression = exprDName;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
}