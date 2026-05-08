using Microsoft.Dafny;
using Expression = Microsoft.Dafny.Expression;

namespace MutDafny.Mutator;

public class MethodCallReplacementMutator(string mutationTargetPos, string replacementMethodName, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private SuffixExpr? _childSuffixExpr;
    
    private Expression CreateMutatedExpression(Expression originalExpr) {
        if (_childSuffixExpr is not ApplySuffix appSufExpr || 
            appSufExpr.Lhs is not NameSegment nSegExpr || 
            AlreadyMutated(appSufExpr.Lhs))
            return originalExpr;
        
        MutantGenerator.NumMutations++;
        MutantGenerator.MutatedNodes.Add(appSufExpr.Lhs);
        nSegExpr.Name = replacementMethodName;
        return appSufExpr;
    }
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
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
        aStmt.Rhss = [new ExprRhs(CreateMutatedExpression(TargetExpression))];
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
        if (IsTarget(suffixExpr) && !AlreadyMutated(suffixExpr.Lhs)) {
            _childSuffixExpr = suffixExpr;
            TargetExpression = suffixExpr;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
}