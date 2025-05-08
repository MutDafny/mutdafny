using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class StmtDeletionMutator(string mutationTargetPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private IfStmt? _parentStmt;
    
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
    protected override void HandleMethod(Method method) {
        if (method.Body == null) return;
        if (IsTarget(method.StartToken.pos, method.EndToken.pos)) {
            method.Body = method.Body is DividedBlockStmt ? 
                new DividedBlockStmt(method.Body.Origin, [], null, []) : 
                new BlockStmt(method.Body.Origin, []);
            return;
        }
        
        base.HandleMethod(method);
    }
    
    protected override void HandleBlock(BlockStmt blockStmt) {
        if (blockStmt is DividedBlockStmt dBlockStmt) {
            HandleBlock(dBlockStmt.BodyInit);
            HandleBlock(dBlockStmt.BodyProper);
            HandleBlock(dBlockStmt.Body);
        } else {
            HandleBlock(blockStmt.Body);
        }
    }
    
    protected override void HandleBlock(List<Statement> statements) {
        foreach (var stmt in statements) {
            if (!IsWorthVisiting(stmt.StartToken.pos, stmt.EndToken.pos)) continue;
            
            HandleStatement(stmt);
            if (!TargetFound()) continue; // else mutate
            TargetStatement = null;
            statements.Remove(stmt);
            return;
        }
    }
    
    protected override void VisitStatement(ConcreteAssignStatement cAStmt) {
        if (IsTarget(cAStmt.StartToken.pos, cAStmt.EndToken.pos)) {
            TargetStatement = cAStmt;
            return;
        }
        base.VisitStatement(cAStmt);
    }
    
    protected override void VisitStatement(ProduceStmt pStmt) {
        if (IsTarget(pStmt.StartToken.pos, pStmt.EndToken.pos)) {
            TargetStatement = pStmt;
            return;
        }
        base.VisitStatement(pStmt);
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (IsTarget(ifStmt.StartToken.pos, ifStmt.EndToken.pos)) {
            TargetStatement = ifStmt;
            return;
        }
        if (IsTarget(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            if (ifStmt.Els != null) {
                RecursivelyMutateIfStmt(ifStmt);
            } else {
                ifStmt.Thn = new BlockStmt(ifStmt.Thn.Origin, []);
            }
            _parentStmt = null;
            return;
        } 
        if (ifStmt.Els != null && IsTarget(ifStmt.Els.StartToken.pos, ifStmt.Els.EndToken.pos)) {
            ifStmt.Els = null;
            _parentStmt = null;
            return;
        } 
        
        _parentStmt = ifStmt.Els is BlockStmt ? null : ifStmt;
        base.VisitStatement(ifStmt);
    }

    private void RecursivelyMutateIfStmt(IfStmt ifStmt) {
        if (_parentStmt == null) return;
        
        if (ifStmt.Els == null) {
            _parentStmt.Els = null;
        } else if (ifStmt.Els is BlockStmt blockStmt) {
            _parentStmt.Els = blockStmt;
        } else if (ifStmt.Els is IfStmt ifStmt2) {
            ifStmt.Thn = ifStmt2.Thn;
            ifStmt.Guard = ifStmt2.Guard;
            _parentStmt = ifStmt;
            RecursivelyMutateIfStmt(ifStmt2);
        }
    }
    
    protected override void VisitStatement(BreakOrContinueStmt bcStmt) {
        if (IsTarget(bcStmt.StartToken.pos, bcStmt.EndToken.pos)) {
            TargetStatement = bcStmt;
            return;
        }
        base.VisitStatement(bcStmt);
    }
    
    protected override void VisitStatement(AlternativeLoopStmt altLStmt) {
        foreach (var alt in altLStmt.Alternatives) {
            if (!IsTarget(alt.Guard.StartToken.pos, alt.Guard.EndToken.pos)) 
                continue;
            altLStmt.Alternatives.Remove(alt);
            return;
        }
        base.VisitStatement(altLStmt);
    }
    
    protected override void VisitStatement(NestedMatchStmt nMatchStmt) {
        foreach (var cs in nMatchStmt.Cases) {
            if (!IsTarget(cs.StartToken.pos, cs.EndToken.pos))
                continue;
            nMatchStmt.Cases.Remove(cs);
            return;
        }
        base.VisitStatement(nMatchStmt);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        foreach (var cs in nMExpr.Cases) {
            if (!IsTarget(cs.StartToken.pos, cs.EndToken.pos))
                continue;
            nMExpr.Cases.Remove(cs);
            return;
        }
        base.VisitExpression(nMExpr);
    }
}