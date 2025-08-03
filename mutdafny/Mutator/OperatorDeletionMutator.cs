using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class OperatorDeletionMutator : ExprReplacementMutator
{
    private BinaryExpr.Opcode _op;
    private readonly bool _shouldDeleteLhs;
        
    public OperatorDeletionMutator(string mutationTargetPos, string val, ErrorReporter reporter) : base(mutationTargetPos, reporter)
    {
        var  args = val.Split('-').ToList();
        if (args.Count != 2) return;
        _op = (BinaryExpr.Opcode)Enum.Parse(typeof(BinaryExpr.Opcode), args[0]);
        _shouldDeleteLhs = args[1] == "left";
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        if (originalExpr is not BinaryExpr bExpr)
            return originalExpr;
        
        return _shouldDeleteLhs ? bExpr.E1 : bExpr.E0;
    }

    private bool IsTarget(BinaryExpr expr) {
        return expr.Op == _op;
    }

    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitExpression(BinaryExpr bExpr) {
        base.VisitExpression(bExpr);
        if (IsTarget(bExpr))
            TargetExpression = bExpr;
    }

    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        return true;
    }
}