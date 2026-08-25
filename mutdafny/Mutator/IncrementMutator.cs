using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class IncrementMutator(string mutationTargetPos, string type, bool isIncrement, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        var binaryOpcode = isIncrement ? BinaryExpr.Opcode.Add : BinaryExpr.Opcode.Sub;
        var oneLiteral = CreateOneLiteral(originalExpr.Origin);
        Expression mutatedExpr;
        if (_chainingExpressionParent != null) {
            var operands = _chainingExpressionParent.Operands;
            foreach (var (e, i) in operands.Select((e, i) => (e, i)).ToList()) {
                if (e != TargetExpression) 
                    continue;
                operands[i] = new BinaryExpr(originalExpr.Origin, binaryOpcode, e, oneLiteral);
            }
            mutatedExpr = new ChainingExpression(_chainingExpressionParent.Origin, operands, 
                _chainingExpressionParent.Operators, _chainingExpressionParent.OperatorLocs, 
                _chainingExpressionParent.PrefixLimits);
            
        } else {
            mutatedExpr = new BinaryExpr(originalExpr.Origin, binaryOpcode, originalExpr, oneLiteral);
        }
        
        TargetExpression = null;
        MutantGenerator.NumMutations++;
        MutantGenerator.MutatedNodes.Add(mutatedExpr);
        return mutatedExpr;
    }

    private LiteralExpr CreateOneLiteral(IOrigin origin) {
        return type == "int" ? 
            new LiteralExpr(origin, 1) : 
            new LiteralExpr(origin, BigDec.FromInt(1));
    }
    
    private bool IsTarget(Expression expr) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return expr.StartToken.pos == startPosition && 
               expr.EndToken.pos == endPosition && 
               !AlreadyMutated(expr) &&
               !ContainsMutatedChildren(expr);
    }
    
    /// ------------------
    /// Overriden visitors
    /// ------------------
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            TargetExpression = bExpr;
            return;
        }
        base.VisitExpression(bExpr);
    }
    
    protected override void VisitExpression(UnaryExpr uExpr) {
        if (IsTarget(uExpr)) {
            TargetExpression = uExpr;
            return;
        }
        base.VisitExpression(uExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var operand in cExpr.Operands) {
            if (IsTarget(operand)) {
                TargetExpression = operand;
                _chainingExpressionParent = cExpr;
                return;
            }
        }
    }
    
    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsTarget(nSegExpr)) {
            TargetExpression = nSegExpr;
            return;
        }
        base.VisitExpression(nSegExpr);
    }
    
    protected override void VisitExpression(LetExpr ltExpr) {
        if (IsTarget(ltExpr)) {
            TargetExpression = ltExpr;
            return;
        }
        base.VisitExpression(ltExpr);
    }
    
    protected override void VisitExpression(LetOrFailExpr ltOrFExpr) {
        if (IsTarget(ltOrFExpr)) {
            TargetExpression = ltOrFExpr;
            return;
        }
        base.VisitExpression(ltOrFExpr);
    }
    
    protected override void VisitExpression(ApplyExpr appExpr) {
        if (IsTarget(appExpr)) {
            TargetExpression = appExpr;
            return;
        }
        base.VisitExpression(appExpr);
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (IsTarget(suffixExpr)) {
            TargetExpression = suffixExpr;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        if (IsTarget(fCallExpr)) {
            TargetExpression = fCallExpr;
            return;
        }
        base.VisitExpression(fCallExpr);
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        if (IsTarget(mSelExpr)) {
            TargetExpression = mSelExpr;
            return;
        }
        base.VisitExpression(mSelExpr);
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        if (IsTarget(iteExpr)) {
            TargetExpression = iteExpr;
            return;
        }
        base.VisitExpression(iteExpr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        if (IsTarget(seqSExpr)) {
            TargetExpression = seqSExpr;
            return;
        }
        base.VisitExpression(seqSExpr);
    }
    
    protected override void VisitExpression(MultiSelectExpr mSExpr) {
        if (IsTarget(mSExpr)) {
            TargetExpression = mSExpr;
            return;
        }
        base.VisitExpression(mSExpr);
    }
}