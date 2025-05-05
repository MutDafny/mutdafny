using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class StmtReplacementMutator(string mutationTargetPos, ErrorReporter reporter) 
    : Mutator(mutationTargetPos, reporter)
{
    protected abstract Statement CreateMutatedStatement(Statement originalStmt);
    
    /// ---------------------------
    /// Group of statement visitors
    /// ---------------------------
    protected override void HandleBlock(List<Statement> statements) {
        for (var i = 0; i < statements.Count; i++) {
            var stmt = statements[i];
            if (!IsWorthVisiting(stmt.StartToken.pos, stmt.EndToken.pos)) continue;
            
            HandleStatement(stmt);
            if (!TargetFound()) continue; // else mutate
            statements[i] = CreateMutatedStatement(stmt);
            return;
        }
    }
    
    protected override void VisitStatement(VarDeclStmt vDeclStmt) {
        if (vDeclStmt.Assign == null) return;
        HandleStatement(vDeclStmt.Assign);
        if (TargetFound()) { // mutate
            var newStmt = CreateMutatedStatement(vDeclStmt.Assign);
            if (newStmt is not ConcreteAssignStatement cAStmt) return;
            vDeclStmt.Assign = cAStmt;
        }
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            HandleExpression(ifStmt.Guard);
        } if (IsWorthVisiting(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            HandleBlock(ifStmt.Thn);
        } if (ifStmt.Els != null && IsWorthVisiting(ifStmt.Els.StartToken.pos, ifStmt.Els.EndToken.pos)) {
            if (ifStmt.Els is BlockStmt bEls) {
                HandleBlock(bEls);
            } else if (ifStmt.Els != null) {
                HandleStatement(ifStmt.Els);
                if (TargetFound()) // mutate
                    ifStmt.Els = CreateMutatedStatement(ifStmt.Els);
            }
        }
    }
    
    protected override void VisitStatement(ForallStmt forStmt) {
        if (IsWorthVisiting(forStmt.Range.StartToken.pos, forStmt.Range.EndToken.pos)) {
            HandleExpression(forStmt.Range);
        } if (IsWorthVisiting(forStmt.Body.StartToken.pos, forStmt.Body.EndToken.pos)) {
            HandleStatement(forStmt.Body);
            if (TargetFound()) // mutate
                forStmt.Body = CreateMutatedStatement(forStmt.Body);
        }
    }
    
    protected override void VisitStatement(ModifyStmt mdStmt) {
        HandleStatement(mdStmt.Body);
        if (TargetFound()) { // mutate
            var newStmt = CreateMutatedStatement(mdStmt.Body);
            if (newStmt is not BlockStmt bStmt) return;
            mdStmt.Body = bStmt;
        }
    }
    
    // protected override void VisitStatement(BlockByProofStmt bBpStmt) {
    //     HandleStatement(bBpStmt.Body);
    //     if (TargetFound()) // mutate
    //         bBpStmt.Body = CreateMutatedStatement(bBpStmt.Body);
    // }

    protected override void VisitStatement(SkeletonStatement skStmt) {
        if (skStmt.S == null) return;
        HandleStatement(skStmt.S);
        if (TargetFound()) // mutate
            skStmt.S = CreateMutatedStatement(skStmt.S);
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        HandleStatement(stmtExpr.S);
        if (TargetFound()) { // mutate
            stmtExpr.S = CreateMutatedStatement(stmtExpr.S);
            return;
        }
        HandleExpression(stmtExpr.E); 
    }
}