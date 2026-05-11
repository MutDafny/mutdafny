using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class ConditionalBlockExtractionMutator(string mutationTargetPos, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private bool _isElseBlock;
    private IfStmt? _parentIfStmt;
    private ITEExpr? _parentITEExpr;

    private List<Statement> CreateMutatedStatement() {
        List<Statement> statements = new List<Statement>();
        if (TargetStatement is BlockStmt bStmt) {
            statements = bStmt.Body;
            MutantGenerator.NumMutations++;
            MutantGenerator.MutatedNodes.AddRange(statements);
        } else if (TargetStatement != null) {
            statements.Add(TargetStatement);
            MutantGenerator.NumMutations++;
            MutantGenerator.MutatedNodes.Add(TargetStatement);
        }
        TargetStatement = null;
        return statements;
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        if (originalExpr is not ITEExpr iteExpr)
            return originalExpr;
        var mutatedExpr = _isElseBlock ? iteExpr.Els : iteExpr.Thn;
        MutantGenerator.NumMutations++;
        MutantGenerator.MutatedNodes.Add(mutatedExpr);
        return mutatedExpr;
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
            var newStmts = CreateMutatedStatement();
            statements.RemoveAt(i);
            statements.InsertRange(i, newStmts);
            return;
        }
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        _parentIfStmt ??= ifStmt;
        if (IsTarget(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos) && 
            !AlreadyMutated(_parentIfStmt) && !ContainsMutatedChildren(_parentIfStmt))
        {
            TargetStatement = ifStmt.Thn;
            return;
        }
        if (ifStmt.Els != null && IsTarget(ifStmt.Els.StartToken.pos, ifStmt.Els.EndToken.pos) && 
            !AlreadyMutated(_parentIfStmt) && !ContainsMutatedChildren(_parentIfStmt))
        {
            TargetStatement = ifStmt.Els;
            _isElseBlock = true;
            return;
        }
        base.VisitStatement(ifStmt);
        _parentIfStmt = null;
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        _parentITEExpr ??= iteExpr;
        if (IsTarget(iteExpr.Thn.StartToken.pos, iteExpr.Thn.EndToken.pos) && 
            !AlreadyMutated(_parentITEExpr) && !ContainsMutatedChildren(_parentITEExpr)) 
        {
            TargetExpression = iteExpr.Thn;
            return;
        }
        if (IsTarget(iteExpr.Els.StartToken.pos, iteExpr.Els.EndToken.pos) && 
            !AlreadyMutated(_parentITEExpr) && !ContainsMutatedChildren(_parentITEExpr)) 
        {
            TargetExpression = iteExpr.Els;
            _isElseBlock = true;
            return;
        }
        base.VisitExpression(iteExpr);
        _parentITEExpr = null;
    }
}