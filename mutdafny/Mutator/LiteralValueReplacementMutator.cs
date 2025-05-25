using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator replaces a literal value with another of the same type
public class LiteralValueReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        Expression mutatedExpr = int.TryParse(val, out var intVal) ?
            new LiteralExpr(originalExpr.Origin, intVal) : (
                double.TryParse(val, out _) ? 
                new LiteralExpr(originalExpr.Origin, BigDec.FromString(val)) : 
                new StringLiteralExpr(originalExpr.Origin, val, false)
            );
        
        if (_chainingExpressionParent != null) {
            var operands = _chainingExpressionParent.Operands;
            foreach (var (e, i) in operands.Select((e, i) => (e, i)).ToList()) {
                if (e != TargetExpression) continue;
                operands[i] = mutatedExpr;
            }
            mutatedExpr = new ChainingExpression(_chainingExpressionParent.Origin, operands, 
                _chainingExpressionParent.Operators, _chainingExpressionParent.OperatorLocs, 
                _chainingExpressionParent.PrefixLimits);
        }

        TargetExpression = null;
        return mutatedExpr;
    }
    
    private bool IsTarget(LiteralExpr expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitExpression(LiteralExpr litExpr) {
        if (IsTarget(litExpr)) {
            TargetExpression = litExpr;
            return;
        }
        base.VisitExpression(litExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var operand in cExpr.Operands) {
            if (operand is LiteralExpr litExpr && IsTarget(litExpr)) {
                TargetExpression = operand;
                _chainingExpressionParent = cExpr;
                return;
            }
        }
    }
}