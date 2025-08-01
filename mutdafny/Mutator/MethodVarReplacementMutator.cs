using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MethodVarReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private readonly List<string> _vars = val.Split('-').ToList();
    private bool _isAssignReplacement;
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        return new NameSegment(originalExpr.Origin, _vars[0], null);
    }

    private NameSegment CreateMutatedExpression(Expression originalExpr, string var) {
        return new NameSegment(originalExpr.Origin, var, null);
    }
    
    private List<AssignmentRhs> CreateMutatedRhss() {
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
        _isAssignReplacement = true;
        base.VisitStatement(aStmt);
        _isAssignReplacement = false;
        if (TargetExpression == null) return; // target not found
        aStmt.Rhss = CreateMutatedRhss();
        TargetExpression = null;
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        _isAssignReplacement = true;
        base.VisitStatement(aStStmt);
        _isAssignReplacement = false;
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