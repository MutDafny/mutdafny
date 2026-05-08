using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class SubseqLimitDeletionMutator(string mutationTargetPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos) && 
               !AlreadyMutated(expr) && !ContainsMutatedChildren(expr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        if (seqSExpr.E0 != null && IsTarget(seqSExpr.E0)) {
            MutantGenerator.NumMutations++;
            MutantGenerator.MutatedNodes.Add(seqSExpr);
            seqSExpr.E0 = null;
            return;
        }
        if (seqSExpr.E1 != null && IsTarget(seqSExpr.E1)) {
            MutantGenerator.NumMutations++;
            MutantGenerator.MutatedNodes.Add(seqSExpr);
            seqSExpr.E1 = null;
            return;
        }
        base.VisitExpression(seqSExpr);
    }
}