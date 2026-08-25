using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class SequenceConcatenationArgSwap(string mutationTargetPos, ErrorReporter reporter) 
    : Mutator(mutationTargetPos, reporter)
{
    private void Mutate(BinaryExpr bExpr) {
        var cloner = new Cloner();
        var lhsOp = cloner.CloneExpr(bExpr.E0);
        var rhsOp = cloner.CloneExpr(bExpr.E1);
        bExpr.E0 = rhsOp;
        bExpr.E1 = lhsOp;
    }
    
    private bool IsTarget(BinaryExpr expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos) && !AlreadyMutated(expr);
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            MutantGenerator.NumMutations++;
            MutantGenerator.MutatedNodes.Add(bExpr);
            TargetExpression = bExpr;
            Mutate(bExpr);
            return;
        }
        base.VisitExpression(bExpr);
    }
}