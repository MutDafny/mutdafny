using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

public class OperatorDeletionMutator : ExprReplacementMutator
{
    private BinaryExpr.Opcode _op;
    private readonly bool _shouldDeleteLhs;
    private Type? _currentTypeRestriction;
    private ConcreteAssignStatement? _parentAssignStmt;
        
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
        if (_currentTypeRestriction != null && mutatedExpr.Type.ToString() != _currentTypeRestriction.ToString())
            return originalExpr;
        return mutatedExpr;
    }

    private bool IsTarget(BinaryExpr expr) {
        return expr.Op == _op;
    }

    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitStatement(AssignStatement aStmt) {
        _parentAssignStmt = aStmt;
        base.VisitStatement(aStmt);
        _parentAssignStmt = null;
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        _parentAssignStmt = aStStmt;
        base.VisitStatement(aStStmt);
        _parentAssignStmt = null;
    }
    
    protected override void VisitStatement(AssignOrReturnStmt aOrRStmt) {
        _parentAssignStmt = aOrRStmt;
        base.VisitStatement(aOrRStmt);
        _parentAssignStmt = null;
    }
    
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
    
    protected override void HandleRhsList(List<AssignmentRhs> rhss) {
        foreach (var (rhs, i) in rhss.Select((rhs, i) => (rhs, i))) {
            if (!IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos))
                continue;
            _currentTypeRestriction = _parentAssignStmt?.Lhss[i].Type;
            HandleAssignmentRhs(rhs);
            _currentTypeRestriction = null;
        }
    }

    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        return true;
    }
}