using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator replaces a relational or conditional binary expression with true or false
public class BinaryOpBoolMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            TargetExpression = bExpr;
            return;
        }
        base.VisitExpression(bExpr);
    }

    private bool IsTarget(BinaryExpr expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        return new LiteralExpr(originalExpr.Origin, bool.Parse(val));
    }
}