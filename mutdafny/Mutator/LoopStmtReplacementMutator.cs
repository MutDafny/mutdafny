using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator replaces a continue statement with a break or a break statement with either a continue or return
public class LoopStmtReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : StmtReplacementMutator(mutationTargetPos, reporter)
{
    protected override Statement CreateMutatedStatement(Statement originalStmt) {
        TargetStatement = null;
        var origin  = originalStmt.Origin;
        var attributes = originalStmt.Attributes;
        
        if (originalStmt is not BreakOrContinueStmt bcStmt) 
            return new ReturnStmt(origin, null);
        var count = bcStmt.BreakAndContinueCount;
        return val switch {
            "break" => new BreakOrContinueStmt(origin, count, false, attributes),
            "continue" => new BreakOrContinueStmt(origin, count, true, attributes),
            _ => new ReturnStmt(origin, null)
        };
    }

    private bool IsTarget(BreakOrContinueStmt stmt) {
        return stmt.Center.pos == int.Parse(MutationTargetPos);
    }

    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitStatement(BreakOrContinueStmt bcStmt) {
        if (IsTarget(bcStmt)) {
            TargetStatement = bcStmt;
            return;
        }
        base.VisitStatement(bcStmt);
    }
}