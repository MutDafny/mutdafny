using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class CaseBlockReplacementMutator(string mutationTargetPos, ErrorReporter reporter): Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(NestedMatchCase cs) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return cs.StartToken.pos == startPosition && cs.EndToken.pos == endPosition;
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitStatement(NestedMatchStmt nMatchStmt) {
        NestedMatchCaseStmt? defaultCase = null;
        NestedMatchCaseStmt? targetCase = null;
        var cloner = new Cloner();
        foreach (var cs in nMatchStmt.Cases) {
            if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) {
                defaultCase = cs.Clone(cloner);
            } else if (IsTarget(cs)) {
                targetCase = cs.Clone(cloner);
                TargetNestedMatchCase = cs;
            }
        }

        if (defaultCase == null || targetCase == null) return;
        foreach (var cs in nMatchStmt.Cases) {
            if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) {
                cs.Body = targetCase.Body;
            } else {
                cs.Body = defaultCase.Body;
            }
        }

        if (TargetFound())
            return;
        base.VisitStatement(nMatchStmt);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        NestedMatchCaseExpr? defaultCase = null;
        NestedMatchCaseExpr? targetCase = null;
        foreach (var cs in nMExpr.Cases) {
            if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) {
                defaultCase = new NestedMatchCaseExpr(cs.Origin, cs.Pat, cs.Body, cs.Attributes);
            } else if (IsTarget(cs)) {
                targetCase = new NestedMatchCaseExpr(cs.Origin, cs.Pat, cs.Body, cs.Attributes);;
                TargetNestedMatchCase = cs;
            }
        }

        if (defaultCase == null || targetCase == null) return;
        foreach (var cs in nMExpr.Cases) {
            if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) {
                cs.Body = targetCase.Body;
            } else {
                cs.Body = defaultCase.Body;
            }
        }

        if (TargetFound())
            return;
        base.VisitExpression(nMExpr);
    }
}