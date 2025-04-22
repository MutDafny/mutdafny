using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator replaces a literal value with another of the same type
public class LiteralValueReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
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
                double.TryParse(val, out _) ? 
                new LiteralExpr(originalExpr.Origin, BigDec.FromString(val)) : 
                new StringLiteralExpr(originalExpr.Origin, val, false)
            );
    }
}