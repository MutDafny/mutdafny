using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MethodCallReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : Mutator(mutationTargetPos, reporter)
{
    private List<AssignmentRhs> CreateMutatedRhs(Expression originalRhs) {
        var rhss = new List<AssignmentRhs>();
        var types = val.Split('-').ToList();
        
        foreach (var type in types) {
            var newExprRhs = new ExprRhs(CreateMutatedExpression(type, originalRhs));
            rhss.Add(newExprRhs);
        }
        return rhss;
    }
    
    private Expression CreateMutatedExpression(string type, Expression originalExpr) {
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
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    /// -----------------
    /// Overriden visitor
    /// -----------------
    protected override void VisitStatement(AssignStatement aStmt) {
        base.VisitStatement(aStmt);
        if (TargetExpression == null) return; // target not found
        aStmt.Rhss = CreateMutatedRhs(TargetExpression);
        TargetExpression = null;
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        base.VisitStatement(aStStmt);
        if (TargetExpression == null) return; // target not found
        aStStmt.Expr = CreateMutatedExpression(val, aStStmt.Expr);
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