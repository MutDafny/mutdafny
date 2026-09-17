using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MethodReturnReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private readonly List<string> _types = val.Split('-').ToList();
    private SuffixExpr? _childSuffixExpr;
    private bool _isAssignReplacement;
    private bool _alreadyCountedMut;
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos) && 
               !AlreadyMutated(expr) && !ContainsMutatedChildren(expr);
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        var mutatedExpr = originalExpr;
        if (_types.Count != 0 || _childSuffixExpr == null || _childSuffixExpr is not ApplySuffix appSufExpr)
            mutatedExpr = CreateDefaultExpression(_types[0], originalExpr.Origin);
        if (!_alreadyCountedMut) {
            MutantGenerator.NumMutations++;
            _alreadyCountedMut = true;
        }
        MutantGenerator.MutatedNodes.Add(mutatedExpr);
        ForbidChildrenMutation(mutatedExpr);
        return mutatedExpr;
    }
    
    private List<AssignmentRhs> CreateMutatedRhss(Expression originalRhs) {
        if (_types.Count != 0)
            return CreateDefaultRhss(originalRhs);
        return [new ExprRhs(originalRhs)];
    }
    
    private List<AssignmentRhs> CreateDefaultRhss(Expression originalRhs) {
        var rhss = new List<AssignmentRhs>();
        foreach (var type in _types)
        {
            var newRhs = CreateDefaultExpression(type, originalRhs.Origin);
            var newExprRhs = new ExprRhs(newRhs);
            rhss.Add(newExprRhs);
            if (!_alreadyCountedMut) {
                MutantGenerator.NumMutations++;
                _alreadyCountedMut = true;
            }
            MutantGenerator.MutatedNodes.Add(newRhs);
        }
        return rhss; 
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member is not ConstantField cf)
                continue;
            if (cf.Rhs != null && IsTarget(cf.Rhs)) {
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
        aStmt.Rhss = CreateMutatedRhss(TargetExpression);
        aStmt.Rhss.ForEach(ForbidChildrenMutation);
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