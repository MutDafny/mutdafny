using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

public class OperatorDeletionMutator : ExprReplacementMutator
{
    private BinaryExpr.Opcode _op;
    private readonly bool _shouldDeleteLhs;
    private Type? _currentTypeRestriction;
        
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
        if (_currentTypeRestriction != null && mutatedExpr.GetType() != _currentTypeRestriction.GetType())
            return originalExpr;
        return mutatedExpr;
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
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null) {
            _currentTypeRestriction = new BoolType();
            HandleExpression(ifStmt.Guard);
            if (TargetFound()) // mutate
                ifStmt.Guard = CreateMutatedExpression(ifStmt.Guard);
            _currentTypeRestriction = null;
        }
        HandleBlock(ifStmt.Thn);
        if (ifStmt.Els is BlockStmt bEls) {
            HandleBlock(bEls);
        } else if (ifStmt.Els != null) {
            HandleStatement(ifStmt.Els);
        }
    }
    
    protected override void VisitStatement(WhileStmt whileStmt) {
        _currentTypeRestriction = new BoolType();
        HandleExpression(whileStmt.Guard);
        if (TargetFound()) // mutate
            whileStmt.Guard = CreateMutatedExpression(whileStmt.Guard);
        _currentTypeRestriction = null;
        if (whileStmt.Body != null) HandleBlock(whileStmt.Body); 
    }
    
    protected override void HandleGuardedAlternatives(List<GuardedAlternative> alternatives) {
        foreach (var alt in alternatives) {
            _currentTypeRestriction = new BoolType();
            HandleExpression(alt.Guard);
            if (TargetFound()) // mutate
                alt.Guard = CreateMutatedExpression(alt.Guard);
            _currentTypeRestriction = null;
            HandleBlock(alt.Body);  
        }
    }

    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        return true;
    }
}