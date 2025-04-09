using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator deletes unary operators, such as the arithmetic - and the logical/conditional !
public class UnaryOpDeletionMutator(string mutationTargetPos, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;
    
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
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var operand in cExpr.Operands) {
            if (IsTarget(operand)) {
                TargetExpression = operand;
                _chainingExpressionParent = cExpr;
                return;
            }
        }
    }
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        Expression mutatedExpr;
        if (_chainingExpressionParent != null) {
            var operands = _chainingExpressionParent.Operands;
            foreach (var (e, i) in operands.Select((e, i) => (e, i)).ToList()) {
                if (e != TargetExpression || TargetExpression is not NegationExpression nExpr) 
                    continue;
                operands[i] = nExpr.E;
            }
            mutatedExpr = new ChainingExpression(_chainingExpressionParent.Origin, operands, 
                _chainingExpressionParent.Operators, _chainingExpressionParent.OperatorLocs, 
                _chainingExpressionParent.PrefixLimits);
            
        } else if (TargetExpression is UnaryExpr uExpr) {
            mutatedExpr = uExpr.E;
        } else {
            var nExpr = TargetExpression as NegationExpression;
            mutatedExpr = nExpr!.E;
        }
       
        TargetExpression = null;
        return mutatedExpr;
    }
}