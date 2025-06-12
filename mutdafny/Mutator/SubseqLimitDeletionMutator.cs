using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class SubseqLimitDeletionMutator(string mutationTargetPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        if (seqSExpr.E0 != null && IsTarget(seqSExpr.E0)) {
            seqSExpr.E0 = null;
            return;
        }
        if (seqSExpr.E1 != null && IsTarget(seqSExpr.E1)) {
            seqSExpr.E1 = null;
            return;
        }
        base.VisitExpression(seqSExpr);
    }
}