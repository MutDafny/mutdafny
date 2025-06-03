using DAST;
using Microsoft.Dafny;
using Statement = Microsoft.Dafny.Statement;

namespace MutDafny.Mutator;

public class SwapStmtMutator(string mutationTargetPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private bool IsTarget(Statement stmt) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return stmt.StartToken.pos == startPosition && stmt.EndToken.pos == endPosition;
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void HandleBlock(List<Statement> statements) {
        foreach (var (stmt, i) in statements.Select((stmt, i) => (stmt, i)).ToList()) {
            if (!IsTarget(stmt)) continue;
            TargetStatement = stmt;

            if (i == 0) return;
            var prevStmt = CloneStatement(statements[i - 1]);
            if (prevStmt == null) return;
            statements[i - 1] = statements[i];
            statements[i] = prevStmt;
            return;
        }
        base.HandleBlock(statements);
    }

    private Statement? CloneStatement(Statement original) {
        var cloner = new Cloner();
        
        return original switch {
            BlockStmt blockStmt => new BlockStmt(original.Origin, blockStmt.Body),
            AssignStatement aStmt => new AssignStatement(cloner, aStmt),
            AssignSuchThatStmt aStStmt => new AssignSuchThatStmt(cloner, aStStmt),
            AssignOrReturnStmt aOrRStmt => new AssignOrReturnStmt(cloner, aOrRStmt),
            SingleAssignStmt sAStmt => new SingleAssignStmt(cloner, sAStmt),
            VarDeclStmt vDeclStmt => new VarDeclStmt(cloner, vDeclStmt),
            VarDeclPattern vDeclPStmt => new VarDeclPattern(cloner, vDeclPStmt),
            ReturnStmt rStmt => new ReturnStmt(cloner, rStmt),
            YieldStmt yStmt => new YieldStmt(cloner, yStmt),
            IfStmt ifStmt => new IfStmt(cloner, ifStmt),
            WhileStmt whileStmt => new WhileStmt(cloner, whileStmt),
            ForLoopStmt forStmt => new ForLoopStmt(cloner, forStmt),
            ForallStmt forStmt => new ForallStmt(cloner, forStmt),
            BreakOrContinueStmt bcStmt => new BreakOrContinueStmt(cloner, bcStmt),
            AlternativeLoopStmt altLStmt => new AlternativeLoopStmt(cloner, altLStmt),
            AlternativeStmt altStmt => new AlternativeStmt(cloner, altStmt),
            MatchStmt matchStmt => new MatchStmt(cloner, matchStmt),
            NestedMatchStmt nMatchStmt => new NestedMatchStmt(cloner, nMatchStmt),
            CallStmt callStmt => new CallStmt(cloner, callStmt),
            ModifyStmt mdStmt => new ModifyStmt(cloner, mdStmt),
            HideRevealStmt hRStmt => new HideRevealStmt(cloner, hRStmt),
            BlockByProofStmt bBpStmt => new BlockByProofStmt(cloner, bBpStmt),
            SkeletonStatement skStmt => new SkeletonStatement(cloner, skStmt),
            _ => null
        };
    }
}