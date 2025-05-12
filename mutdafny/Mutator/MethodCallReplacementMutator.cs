using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Expression = Microsoft.Dafny.Expression;
using LiteralExpr = Microsoft.Dafny.LiteralExpr;

namespace MutDafny.Mutator;

public class MethodCallReplacementMutator : Mutator
{
    private readonly List<string> _types = [];
    private readonly List<int> _replacementArgsPos = [];
    private SuffixExpr? _childSuffixExpr;
    
    public MethodCallReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) : base(mutationTargetPos, reporter)
    {
        var  args = val.Split('-').ToList();
        if (int.TryParse(args[0], out _)) {
            _replacementArgsPos = args.Select(int.Parse).ToList();
        } else if (val != "") {
            _types = args;
        }
    }

    private Expression CreateMutatedExpression(Expression originalExpr) {
        if (_types.Count != 0 || _childSuffixExpr == null || _childSuffixExpr is not ApplySuffix appSufExpr)
            return CreateDefaultExpression(_types[0], originalExpr);
        if (_types.Count == 0 && _replacementArgsPos.Count == 0 &&
            _childSuffixExpr is ExprDotName exprDName) // naked receiver
            return exprDName.Lhs;
        return appSufExpr.Bindings.ArgumentBindings[_replacementArgsPos[0]].Actual; // argument propagation
    }
    
    private List<AssignmentRhs> CreateMutatedRhss(Expression originalRhs) {
        if (_types.Count != 0)
            return CreateDefaultRhss(originalRhs);
        if (_types.Count == 0 && _replacementArgsPos.Count == 0 &&
            _childSuffixExpr is ExprDotName exprDName) // naked receiver
            return [new ExprRhs(exprDName.Lhs)];
        return CreateArgumentPropagationRhss();
    }

    private List<AssignmentRhs> CreateDefaultRhss(Expression originalRhs) {
        var rhss = new List<AssignmentRhs>();
        foreach (var type in _types) {
            var newExprRhs = new ExprRhs(CreateDefaultExpression(type, originalRhs));
            rhss.Add(newExprRhs);
        }
        return rhss; 
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
    
    private bool IsTarget(Expression expr) {
        return expr.Center.pos == int.Parse(MutationTargetPos);
    }
    
    /// -----------------
    /// Group of overriden visitor
    /// -----------------
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