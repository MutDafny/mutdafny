using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

public class ThisKeywordDeletionMutator(string mutationTargetPos, ErrorReporter reporter): ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        var nameValue = TargetExpression is ExprDotName exprDName ? exprDName.SuffixName : "";
        Expression mutatedExpr = new NameSegment(originalExpr.Origin, nameValue, null);
        
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