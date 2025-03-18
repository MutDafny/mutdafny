using Microsoft.Dafny;

namespace MutDafny.Visitor;

// this type of finder is used to find the statement in which specific binary operators are used
public class BinaryOpTargetFinder(int mutationTargetPos, ErrorReporter reporter) : Visitor(mutationTargetPos, reporter)
{
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            TargetExpression = bExpr;
            return;
        }
        List<Expression> exprs = [bExpr.E0, bExpr.E1];
        HandleExprList(exprs);
    }
    
    private bool IsTarget(BinaryExpr expr) {
        return expr.Center.pos == MutationTargetPos;
    }
}