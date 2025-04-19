using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class PostResolveTargetScanner(ErrorReporter reporter) : TargetScanner(reporter)
{
    private bool _skipChildMutation;
    
    private void HandleType(Expression expr) {
        if (_skipChildMutation) {
            _skipChildMutation = false;
            return;
        }
        
        switch (expr.Type) {
            case IntType:
            case RealType:
                Targets.Add(($"{expr.StartToken.pos}-{expr.EndToken.pos}", "UOI", "Minus")); 
                break;
            case BoolType:
            case BitvectorType:
                Targets.Add(($"{expr.StartToken.pos}-{expr.EndToken.pos}", "UOI", UnaryOpExpr.Opcode.Not.ToString()));
                break;
        }
    }

    private void HandleType(LiteralExpr litExpr) {
        HandleType(litExpr as Expression);

        switch (litExpr.Type) {
            case IntType:
                HandleIntegerLiteral(litExpr); break;
            case RealType:
                HandleRealLiteral(litExpr); break;
            default:
                if (litExpr is StringLiteralExpr) {
                    HandleStringLiteral(litExpr);
                }
                break;
        }
    }

    private void HandleIntegerLiteral(LiteralExpr litExpr) {
        if (!int.TryParse(litExpr.Value.ToString(), out var numVal))
            return;
        
        Targets.Add(($"{litExpr.Center.pos}", "LVR", $"{numVal + 1}"));
        Targets.Add(($"{litExpr.Center.pos}", "LVR", $"{numVal - 1}"));
        if (numVal == 0 || numVal + 1 == 0 || numVal - 1 == 0)
            return;
        Targets.Add(($"{litExpr.Center.pos}", "LVR", $"0"));
    }
    
    private void HandleRealLiteral(LiteralExpr litExpr) {
        if (!double.TryParse(litExpr.Value.ToString(), out var numVal))
            return;
        var decimalPlaces = BitConverter.GetBytes(decimal.GetBits((decimal)numVal)[3])[2];
        var format = "0." + new string('0', decimalPlaces);
        
        var incVal = (numVal + 1).ToString(format);
        incVal = incVal.Contains('.') ? incVal : incVal + ".0";
        Targets.Add(($"{litExpr.Center.pos}", "LVR", incVal));

        var decVal = (numVal - 1).ToString(format);
        decVal = decVal.Contains('.') ? decVal : decVal + ".0";
        Targets.Add(($"{litExpr.Center.pos}", "LVR", decVal));
        
        if (numVal == 0 || numVal + 1 == 0 || numVal - 1 == 0)
            return;
        Targets.Add(($"{litExpr.Center.pos}", "LVR", $"0.0"));
    } 

    private void HandleStringLiteral(LiteralExpr litExpr) {
        var sVal = litExpr.Value.ToString();
        if (sVal == null) return;
        
        var repVal = sVal == "" ? "MutDafny" : "";
        Targets.Add(($"{litExpr.Center.pos}", "LVR", repVal));
        if (sVal.Length <= 1) return;
        Targets.Add(($"{litExpr.Center.pos}", "LVR", 
            sVal[0] + "XX" + 
            sVal.Substring(1, sVal.Length - 2) + 
            "XX" + sVal[^1]));
    }
   
    /// -------------------------------------
    /// Group of overriden statement visitors
    /// -------------------------------------
    protected override void VisitStatement(ConcreteAssignStatement cAStmt) { }
    
    protected override void VisitStatement(SingleAssignStmt sAStmt) {
        HandleRhsList([sAStmt.Rhs]);
    }

    /// --------------------------------------
    /// Group of overriden expression visitors
    /// --------------------------------------
    protected override void VisitExpression(LiteralExpr litExpr) {
        HandleType(litExpr);
    }
    protected override void VisitExpression(BinaryExpr bExpr) {
        HandleType(bExpr);
        base.VisitExpression(bExpr);
    }
    
    protected override void VisitExpression(UnaryExpr uExpr) {
        _skipChildMutation = true;
        base.VisitExpression(uExpr);
    }
    
    protected override void VisitExpression(ParensExpression pExpr) {
        HandleType(pExpr);
        _skipChildMutation = true;
        base.VisitExpression(pExpr);
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        _skipChildMutation = true;
        base.VisitExpression(nExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        HandleType(cExpr);
        foreach (var operand in cExpr.Operands) {
            if (operand is not NegationExpression)
                HandleType(operand);
        }
    }

    protected override void VisitExpression(NameSegment nSegExpr) {
        HandleType(nSegExpr);
    }
    
    protected override void VisitExpression(LetExpr ltExpr) {
        HandleType(ltExpr);
        base.VisitExpression(ltExpr);
    }
    
    protected override void VisitExpression(LetOrFailExpr ltOrFExpr) {
        HandleType(ltOrFExpr);
        base.VisitExpression(ltOrFExpr);
    }
    
    protected override void VisitExpression(ApplyExpr appExpr) {
        HandleType(appExpr);
        base.VisitExpression(appExpr);
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        HandleType(suffixExpr);
        base.VisitExpression(suffixExpr);
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        HandleType(fCallExpr);
        base.VisitExpression(fCallExpr);
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        HandleType(mSelExpr);
        base.VisitExpression(mSelExpr);
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        HandleType(iteExpr);
        base.VisitExpression(iteExpr);
    }
    
    protected override void VisitExpression(MatchExpr mExpr) {
        HandleType(mExpr);
        base.VisitExpression(mExpr);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        HandleType(nMExpr);
        base.VisitExpression(nMExpr);
    }
    
    protected override void VisitExpression(DisplayExpression dExpr) {
        HandleType(dExpr);
        base.VisitExpression(dExpr);
    }
    
    protected override void VisitExpression(MapDisplayExpr mDExpr) {
        HandleType(mDExpr);
        base.VisitExpression(mDExpr);
    }
    
    protected override void VisitExpression(SeqConstructionExpr seqCExpr) {
        HandleType(seqCExpr);
        base.VisitExpression(seqCExpr);
    }
    
    protected override void VisitExpression(MultiSetFormingExpr mSetFExpr) {
        HandleType(mSetFExpr);
        base.VisitExpression(mSetFExpr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        HandleType(seqSExpr);
        base.VisitExpression(seqSExpr);
    }
    
    protected override void VisitExpression(MultiSelectExpr mSExpr) {
        HandleType(mSExpr);
        base.VisitExpression(mSExpr);
    }
    
    protected override void VisitExpression(SeqUpdateExpr seqUExpr) {
        HandleType(seqUExpr);
        base.VisitExpression(seqUExpr);
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        HandleType(compExpr);
        base.VisitExpression(compExpr);
    }
    
    protected override void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        HandleType(dtUExpr);
        base.VisitExpression(dtUExpr);
    }
    
    protected override void VisitExpression(DatatypeValue dtValue) {
        HandleType(dtValue);
        base.VisitExpression(dtValue);
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        HandleType(stmtExpr);
        base.VisitExpression(stmtExpr);
    }
}