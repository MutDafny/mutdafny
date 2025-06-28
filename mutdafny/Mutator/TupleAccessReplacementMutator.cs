using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class TupleAccessReplacementMutator(string mutationTargetPos, string index, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        if (originalExpr is not ExprDotName exprDName)
            return originalExpr;
        
        return new ExprDotName(
            originalExpr.Origin, exprDName.Lhs, 
            new Name(originalExpr.Origin, index), 
            null
        );
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