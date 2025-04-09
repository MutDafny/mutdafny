using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class UnaryOpInsertionMutator(string mutationTargetPos, string op, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;
    
    private bool IsTarget(Expression expr) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return expr.StartToken.pos == startPosition && expr.EndToken.pos == endPosition;
    }
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        Expression mutatedExpr;
        if (_chainingExpressionParent != null) {
            var operands = _chainingExpressionParent.Operands;
            foreach (var (e, i) in operands.Select((e, i) => (e, i)).ToList()) {
                if (e != TargetExpression) continue;
                operands[i] = new NegationExpression(e.Origin, e);
            }
            mutatedExpr = new ChainingExpression(_chainingExpressionParent.Origin, operands, 
                _chainingExpressionParent.Operators, _chainingExpressionParent.OperatorLocs, 
                _chainingExpressionParent.PrefixLimits);
            
        } else if (op == UnaryOpExpr.Opcode.Not.ToString()) {
            mutatedExpr = new UnaryOpExpr(originalExpr.Origin, UnaryOpExpr.Opcode.Not, originalExpr);
        } else {
            mutatedExpr = new NegationExpression(originalExpr.Origin, originalExpr);
        }
       
        TargetExpression = null;
        _chainingExpressionParent = null;
        return mutatedExpr;
    }

    /// ----------------------------
    /// Group of expression visitors
    /// ----------------------------
    protected override void VisitExpression(LiteralExpr litExpr) {
        if (IsTarget(litExpr)) {
            TargetExpression = litExpr;
        }
    }
    
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
    
    protected override void VisitExpression(ParensExpression pExpr) {
        if (IsTarget(pExpr)) {
            TargetExpression = pExpr;
            return;
        }
        base.VisitExpression(pExpr);
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        if (IsTarget(nExpr)) {
            TargetExpression = nExpr;
            return;
        }
        base.VisitExpression(nExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        if (IsTarget(cExpr)) {
            TargetExpression = cExpr;
            return;
        }
        
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
        }
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
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        if (IsTarget(nMExpr)) {
            TargetExpression = nMExpr;
            return;
        }
        base.VisitExpression(nMExpr);
    }
    
    protected override void VisitExpression(MapDisplayExpr mDExpr) {
        if (IsTarget(mDExpr)) {
            TargetExpression = mDExpr;
            return;
        }
        base.VisitExpression(mDExpr);
    }
    
    protected override void VisitExpression(SeqConstructionExpr seqCExpr) {
        if (IsTarget(seqCExpr)) {
            TargetExpression = seqCExpr;
            return;
        }
        base.VisitExpression(seqCExpr);
    }
    
    protected override void VisitExpression(MultiSetFormingExpr mSetFExpr) {
        if (IsTarget(mSetFExpr)) {
            TargetExpression = mSetFExpr;
            return;
        }
        base.VisitExpression(mSetFExpr);
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
    
    protected override void VisitExpression(SeqUpdateExpr seqUExpr) {
        if (IsTarget(seqUExpr)) {
            TargetExpression = seqUExpr;
            return;
        }
        base.VisitExpression(seqUExpr);
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        if (IsTarget(compExpr)) {
            TargetExpression = compExpr;
            return;
        }
        base.VisitExpression(compExpr);
    }
    
    protected override void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        if (IsTarget(dtUExpr)) {
            TargetExpression = dtUExpr;
            return;
        }
        base.VisitExpression(dtUExpr);
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        if (IsTarget(stmtExpr)) {
            TargetExpression = stmtExpr;
            return;
        }
        base.VisitExpression(stmtExpr);
    }
}