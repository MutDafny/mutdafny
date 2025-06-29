using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class SwapVarDeclMutator(string mutationTargetPos, string replacementPos, ErrorReporter reporter) 
    : Mutator(mutationTargetPos, reporter)
{
    private VarDeclStmt? _targetStmt;
    private VarDeclStmt? _replacementStmt;
    
    private bool IsTarget(VarDeclStmt vDeclStmt) {
        return vDeclStmt.Center.pos == int.Parse(MutationTargetPos);
    }
    
    private bool IsReplacement(VarDeclStmt vDeclStmt) {
        return vDeclStmt.Center.pos == int.Parse(replacementPos);
    }

    private void ReplaceStmtsRhss() {
        if (_targetStmt == null || _replacementStmt == null || 
            _targetStmt.Assign is not AssignStatement aStmt1 || 
            _replacementStmt.Assign is not AssignStatement aStmt2)
            return;
        
        List<AssignmentRhs> targetStmtRhs = new List<AssignmentRhs>(aStmt1.Rhss);
        aStmt1.Rhss = aStmt2.Rhss;
        aStmt2.Rhss = targetStmtRhs;
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitStatement(VarDeclStmt vDeclStmt) {
        if (IsTarget(vDeclStmt)) {
            _targetStmt = vDeclStmt;
            TargetStatement = vDeclStmt;
            if (_replacementStmt != null) {
                ReplaceStmtsRhss();
                return;
            }
        }

        if (IsReplacement(vDeclStmt)) {
            _replacementStmt = vDeclStmt;
            if (_targetStmt != null) {
                ReplaceStmtsRhss();
                return;
            }
        }
        
        base.VisitStatement(vDeclStmt);
    }

    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        return true;
    }
}