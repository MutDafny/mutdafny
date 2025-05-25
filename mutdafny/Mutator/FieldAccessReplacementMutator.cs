using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class FieldAccessReplacementMutator(string mutationTargetPos, string field, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;

    private bool IsTarget(ExprDotName exprDName) {
        return exprDName.Center.pos == int.Parse(MutationTargetPos);
    }

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        if (TargetExpression is not ExprDotName exprDName) 
            return originalExpr;

        var newName = new Name(originalExpr.Origin, field);
        Expression mutatedExpr = new ExprDotName(originalExpr.Origin, 
            exprDName.Lhs, newName, exprDName.OptTypeArguments);
        
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
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var operand in cExpr.Operands) {
            if (operand is ExprDotName exprDName && IsTarget(exprDName)) {
                TargetExpression = operand;
                _chainingExpressionParent = cExpr;
                return;
            }
        }
    }
}