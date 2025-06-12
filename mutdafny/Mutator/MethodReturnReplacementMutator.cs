using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MethodReturnReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private readonly List<string> _types = val.Split('-').ToList();
    private SuffixExpr? _childSuffixExpr;
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    private Expression CreateMutatedExpression(Expression originalExpr) {
        if (_types.Count != 0 || _childSuffixExpr == null || _childSuffixExpr is not ApplySuffix appSufExpr)
            return CreateDefaultExpression(_types[0], originalExpr);
        return originalExpr;
    }
    
    private List<AssignmentRhs> CreateMutatedRhss(Expression originalRhs) {
        if (_types.Count != 0)
            return CreateDefaultRhss(originalRhs);
        return [new ExprRhs(originalRhs)];
    }
    
    private List<AssignmentRhs> CreateDefaultRhss(Expression originalRhs) {
        var rhss = new List<AssignmentRhs>();
        foreach (var type in _types) {
            var newExprRhs = new ExprRhs(CreateDefaultExpression(type, originalRhs));
            rhss.Add(newExprRhs);
        }
        return rhss; 
    }
    
    private Expression CreateDefaultExpression(string type, Expression originalExpr) {
        return type switch {
            "int" => new LiteralExpr(originalExpr.Origin, 0),
            "real" => new LiteralExpr(originalExpr.Origin, BigDec.ZERO),
            "bv" => new LiteralExpr(originalExpr.Origin, BigInteger.Zero),
            "bool" => new LiteralExpr(originalExpr.Origin, false),
            "char" => new CharLiteralExpr(originalExpr.Origin, "0"),
            "string" => new StringLiteralExpr(originalExpr.Origin, "", false),
            "set" => new SetDisplayExpr(originalExpr.Origin, true, []),
            "multiset" => new MultiSetDisplayExpr(originalExpr.Origin, []),
            "seq" => new SeqDisplayExpr(originalExpr.Origin, []),
            "map" => new MapDisplayExpr(originalExpr.Origin, true, []),
            _ => new LiteralExpr(originalExpr.Origin, null)
        };
    }
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
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