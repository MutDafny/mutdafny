using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MethodVarReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : Mutator(mutationTargetPos, reporter)
{
    private readonly List<string> _vars = val.Split('-').ToList();

    private NameSegment CreateMutatedExpression(Expression originalExpr, string var) {
        return new NameSegment(originalExpr.Origin, var, null);
    }
    
    private List<AssignmentRhs> CreateArgumentPropagationRhss() {
        var rhss = new List<AssignmentRhs>();
        foreach (var var in _vars) {
            var newExprRhs = new ExprRhs(CreateMutatedExpression(TargetExpression, var));
            rhss.Add(newExprRhs);
        }
        TargetExpression = null;
        return rhss;
    }

    private bool IsTarget(SuffixExpr expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    /// --------------------------
    /// Group of overriden visitor
    /// --------------------------
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member is not ConstantField cf)
                continue;
            if (cf.Rhs is SuffixExpr suffixExpr && IsTarget(suffixExpr)) {
                cf.Rhs = CreateMutatedExpression(cf.Rhs, _vars[0]);
                TargetExpression = null;
                return;
            }
        }
        base.HandleMemberDecls(decl);
    }
    
    protected override void VisitStatement(AssignStatement aStmt) {
        base.VisitStatement(aStmt);
        if (TargetExpression == null) return; // target not found
        aStmt.Rhss = CreateArgumentPropagationRhss();
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        base.VisitStatement(aStStmt);
        if (TargetExpression == null) return; // target not found
        aStStmt.Expr = CreateMutatedExpression(aStStmt.Expr, _vars[0]);
        TargetExpression = null;
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (IsTarget(suffixExpr)) {
            TargetExpression = suffixExpr;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
}