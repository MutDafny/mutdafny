using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator deletes unary operators, such as the arithmetic - and the logical/conditional !
public class UnaryOpDeletionMutator(int mutationTargetPos, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override void VisitExpression(UnaryExpr uExpr) {
        if (IsTarget(uExpr)) {
            TargetExpression = uExpr;
            return;
        }
        base.VisitExpression(uExpr);
    }

    protected override void VisitExpression(NegationExpression nExpr) {
        if (IsTarget(nExpr)) {
            TargetExpression = nExpr;
            return;
        }
        base.VisitExpression(nExpr);
    }
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == MutationTargetPos;
    }

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        Expression mutatedExpr;
        if (TargetExpression is UnaryExpr uExpr) {
            mutatedExpr = uExpr.E;
        } else {
            var nExpr = TargetExpression as NegationExpression;
            mutatedExpr = nExpr!.E;
        }
       
        TargetExpression = null;
        return mutatedExpr;
    }
}