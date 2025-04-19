using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class LiteralExprReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override void VisitExpression(LiteralExpr litExpr) {
        if (IsTarget(litExpr)) {
            TargetExpression = litExpr;
            return;
        }
        base.VisitExpression(litExpr);
    }

    private bool IsTarget(LiteralExpr expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        
        return int.TryParse(val, out var intVal) ?
            new LiteralExpr(originalExpr.Origin, intVal) : (
                double.TryParse(val, out var realVal) ? 
                new LiteralExpr(originalExpr.Origin, BigDec.FromString(val)) : 
                new StringLiteralExpr(originalExpr.Origin, val, false)
            );
    }
}