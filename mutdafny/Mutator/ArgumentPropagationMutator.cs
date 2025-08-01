using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class ArgumentPropagationMutator(string mutationTargetPos, string val, ErrorReporter reporter)
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private readonly List<int> _replacementArgsPos = val.Split('-').Select(int.Parse).ToList();
    private SuffixExpr? _childSuffixExpr;
    private bool _isAssignReplacement;
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        if (_replacementArgsPos.Count == 0 || _childSuffixExpr == null || _childSuffixExpr is not ApplySuffix appSufExpr)
            return originalExpr;
        return appSufExpr.Bindings.ArgumentBindings[_replacementArgsPos[0]].Actual;
    }
    
    private List<AssignmentRhs> CreateArgumentPropagationRhss() {
        if (_childSuffixExpr == null || _childSuffixExpr is not ApplySuffix appSufExpr)
            return [];
        
        var rhss = new List<AssignmentRhs>();
        foreach (var argPos in _replacementArgsPos) {
            var newExprRhs = new ExprRhs(appSufExpr.Bindings.ArgumentBindings[argPos].Actual);
            rhss.Add(newExprRhs);
        }
        return rhss; 
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
        _isAssignReplacement = true;
        base.VisitStatement(aStmt);
        _isAssignReplacement = false;
        if (TargetExpression == null) return; // target not found
        aStmt.Rhss = CreateArgumentPropagationRhss();
        TargetExpression = null;
        _childSuffixExpr = null;
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        _isAssignReplacement = true;
        base.VisitStatement(aStStmt);
        _isAssignReplacement = false;
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
    
    protected override void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
            if (TargetFound() && !_isAssignReplacement) // mutate
                exprRhs.Expr = CreateMutatedExpression(exprRhs.Expr);
        } else if (aRhs is TypeRhs tpRhs) {
            var elInit = tpRhs.ElementInit;
            
            if (tpRhs.ArrayDimensions != null) {
                HandleExprList(tpRhs.ArrayDimensions);
            } if (elInit != null && IsWorthVisiting(elInit.StartToken.pos, elInit.EndToken.pos)) {
                HandleExpression(elInit);
                if (TargetFound() && !_isAssignReplacement) // mutate
                    tpRhs.ElementInit = CreateMutatedExpression(tpRhs.ElementInit);
            } if (tpRhs.InitDisplay != null) {
                HandleExprList(tpRhs.InitDisplay);
            } if (tpRhs.Bindings != null) {
                HandleActualBindings(tpRhs.Bindings);
            }
        }
    }
}