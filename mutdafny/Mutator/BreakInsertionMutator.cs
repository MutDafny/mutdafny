using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class BreakInsertionMutator(string mutationTargetPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private void Mutate(BlockStmt blockStmt) {
        var breakStmt = new BreakOrContinueStmt(blockStmt.Origin, 1, false, null);
        blockStmt.Body.Insert(0, breakStmt);
    }
    
    private bool IsTarget(Statement stmt) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return stmt.StartToken.pos == startPosition && stmt.EndToken.pos == endPosition;
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitStatement(WhileStmt whileStmt) {
        if (IsTarget(whileStmt)) {
            TargetStatement = whileStmt;
            Mutate(whileStmt.Body);
            return;
        }
        base.VisitStatement(whileStmt);
    }

    protected override void VisitStatement(ForLoopStmt forStmt) {
        if (IsTarget(forStmt)) {
            TargetStatement = forStmt;
            Mutate(forStmt.Body);
            return;
        }
        base.VisitStatement(forStmt);
    }
}