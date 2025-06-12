using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class NakedReceiverMutator(string mutationTargetPos, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private SuffixExpr? _childSuffixExpr;

    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    private Expression CreateMutatedExpression(Expression originalExpr) {
        if (_childSuffixExpr is ExprDotName exprDName)
            return exprDName.Lhs;
        return originalExpr;
    }
    
    private List<AssignmentRhs> CreateMutatedRhss(Expression originalRhs) {
        if (_childSuffixExpr is ExprDotName exprDName)
            return [new ExprRhs(exprDName.Lhs)];
        return [new ExprRhs(originalRhs)];
    }
    
    /// --------------------------
    /// Group of overriden visitor
    /// --------------------------
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member is not ConstantField cf)
                continue;
            if (IsTarget(cf.Rhs)) {
                cf.Rhs = CreateMutatedExpression(cf.Rhs);
                return;
            }
        }
        base.HandleMemberDecls(decl);
    }
    
    protected override void VisitStatement(AssignStatement aStmt) {
        base.VisitStatement(aStmt);
        if (TargetExpression == null) return; // target not found
        aStmt.Rhss = CreateMutatedRhss(TargetExpression);
        TargetExpression = null;
        _childSuffixExpr = null;
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        base.VisitStatement(aStStmt);
        if (TargetExpression == null) return; // target not found
        aStStmt.Expr = CreateMutatedExpression(aStStmt.Expr);
        TargetExpression = null;
        _childSuffixExpr = null;
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (IsTarget(suffixExpr)) {
            _childSuffixExpr = suffixExpr;
            TargetExpression = suffixExpr;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
}