using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class OperatorDeletionMutator : ExprReplacementMutator
{
    private BinaryExpr.Opcode _op;
    private readonly bool _shouldDeleteLhs;
    private bool _alreadyCountedMut;
        
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
        
        var mutatedExpr = _shouldDeleteLhs ? bExpr.E1 : bExpr.E0;
        if (!_alreadyCountedMut) {
            MutantGenerator.NumMutations++;
            _alreadyCountedMut = true;
        }
        MutantGenerator.MutatedNodes.Add(mutatedExpr);
        return mutatedExpr;
    }

    private bool IsTarget(BinaryExpr expr) {
        var containsMutatedChildren = _shouldDeleteLhs ? 
            AlreadyMutated(expr.E0) || ContainsMutatedChildren(expr.E0) : 
            AlreadyMutated(expr.E1) || ContainsMutatedChildren(expr.E1);
        return expr.Op == _op && !AlreadyMutated(expr) && !containsMutatedChildren;
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