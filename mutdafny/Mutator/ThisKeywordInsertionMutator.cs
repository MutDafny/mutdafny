using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class ThisKeywordInsertionMutator(string mutationTargetPos, ErrorReporter reporter): ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        var thisExpr = new ThisExpr(originalExpr.Origin);
        var nameValue = TargetExpression is NameSegment nSegExpr ? nSegExpr.Name : "";
        var fieldName = new Name(originalExpr.Origin, nameValue);
        Expression mutatedExpr = new ExprDotName(originalExpr.Origin, thisExpr, fieldName, null);
        
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
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var operand in cExpr.Operands) {
            if (operand is NameSegment nSegExpr && IsTarget(nSegExpr)) {
                TargetExpression = operand;
                _chainingExpressionParent = cExpr;
                return;
            }
        }
    }
}