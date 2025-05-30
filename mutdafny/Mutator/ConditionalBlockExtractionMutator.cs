using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class ConditionalBlockExtractionMutator(string mutationTargetPos, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private bool _isElseBlock;

    private List<Statement> CreateMutatedStatement(Statement originalStmt) {
        TargetStatement = null;
        if (originalStmt is not IfStmt ifStmt || (_isElseBlock && ifStmt.Els is not BlockStmt))
            return [originalStmt];
        return _isElseBlock && ifStmt.Els is BlockStmt bStmt ? bStmt.Body : ifStmt.Thn.Body;
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        if (originalExpr is not ITEExpr iteExpr)
            return originalExpr;
        return _isElseBlock ? iteExpr.Els : iteExpr.Thn;
    }
    
    private bool IsTarget(int startTokenPos, int endTokenPos) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return startTokenPos == startPosition && endTokenPos == endPosition;
    }

    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void HandleBlock(List<Statement> statements) {
        for (var i = 0; i < statements.Count; i++) {
            var stmt = statements[i];
            if (!IsWorthVisiting(stmt.StartToken.pos, stmt.EndToken.pos)) continue;
            
            HandleStatement(stmt);
            if (!TargetFound()) continue; // else mutate
            var newStmts = CreateMutatedStatement(statements[i]);
            statements.RemoveAt(i);
            statements.InsertRange(i, newStmts);
            return;
        }
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (IsTarget(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            TargetStatement = ifStmt;
            return;
        }
        if (ifStmt.Els != null && IsTarget(ifStmt.Els.StartToken.pos, ifStmt.Els.EndToken.pos)) {
            TargetStatement = ifStmt.Els;
            _isElseBlock = true;
            return;
        }
        base.VisitStatement(ifStmt);
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        if (IsTarget(iteExpr.Thn.StartToken.pos, iteExpr.Thn.EndToken.pos)) {
            TargetExpression = iteExpr.Thn;
            return;
        }
        if (IsTarget(iteExpr.Els.StartToken.pos, iteExpr.Els.EndToken.pos)) {
            TargetExpression = iteExpr.Els;
            _isElseBlock = true;
            return;
        }
        base.VisitExpression(iteExpr);
    }
}