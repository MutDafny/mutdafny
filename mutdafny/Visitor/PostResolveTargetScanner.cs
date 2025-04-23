using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Visitor;

public class PostResolveTargetScanner(ErrorReporter reporter) : TargetScanner(reporter)
{
    private bool _skipChildUOIMutation;
    private bool _skipChildEVRMutation;
    
    private void ScanUOITargets(Expression expr) {
        if (_skipChildUOIMutation) {
            _skipChildUOIMutation = false;
            return;
        }
        var exprLocation = $"{expr.StartToken.pos}-{expr.EndToken.pos}";
        
        switch (expr.Type) {
            case IntType:
            case RealType:
                Targets.Add((exprLocation, "UOI", "Minus")); 
                break;
            case BoolType:
            case BitvectorType:
                Targets.Add((exprLocation, "UOI", UnaryOpExpr.Opcode.Not.ToString()));
                break;
        }
    }

    private void ScanLVRTargets(LiteralExpr litExpr) {
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
    
    private void ScanEVRTargets(Expression expr) {
        if (_skipChildEVRMutation) return;
        var exprLocation = $"{expr.StartToken.pos}-{expr.EndToken.pos}";
        
        switch (expr.Type) {
            case IntType:
                Targets.Add((exprLocation, "EVR", "int")); break;
            case RealType:
                Targets.Add((exprLocation, "EVR", "real")); break;
            case BitvectorType:
                Targets.Add((exprLocation, "EVR", "bv")); break;
            case CharType:
                Targets.Add((exprLocation, "EVR", "char")); break;
            case SetType:
                Targets.Add((exprLocation, "EVR", "set")); break;
            case MultiSetType:
                Targets.Add((exprLocation, "EVR", "multiset")); break;
            case SeqType:
                Targets.Add((exprLocation, "EVR", "seq")); break;
            case MapType:
                Targets.Add((exprLocation, "EVR", "map")); break;
            case UserDefinedType uType:
                if (uType.Name == "string") { // string type
                    Targets.Add((exprLocation, "EVR", "string"));
                } else if (expr.Type.IsArrayType) {
                    Targets.Add((exprLocation, "EVR", "array"));
                }
                if (uType.Name[^1] == '?') { // nullable type
                    Targets.Add((exprLocation, "EVR", "null"));
                }
                break;
        }
    }
    
    private void ScanCIRTargets(Expression expr) {
        var exprLocation = $"{expr.StartToken.pos}-{expr.EndToken.pos}";
        string type, arg;
        
        switch (expr) {
            case DisplayExpression dExpr:
                type = TypeToStr(dExpr.Type.TypeArgs[0]);
                if (dExpr.Elements.Count == 0 && type == "") 
                    return;
                arg = dExpr.Elements.Count == 0 ? type : "";
                Targets.Add((exprLocation, "CIR", arg)); break;
            case MapDisplayExpr mDExpr:
                type = $"{TypeToStr(mDExpr.Type.TypeArgs[0])}-{TypeToStr(mDExpr.Type.TypeArgs[1])}";
                if (mDExpr.Elements.Count == 0 && type == "") 
                    return;
                arg = mDExpr.Elements.Count == 0 ? type : "";
                Targets.Add((exprLocation, "CIR", arg)); break;
        }
    }

    private string TypeToStr(Type type) {
        if (type.IsIntegerType) return "int";
        if (type.IsRealType) return "real";
        if (type.IsBitVectorType) return "bv";
        if (type.IsBoolType) return "bool";
        if (type.IsCharType) return "char";
        return type.IsStringType ? "string" : "";
    }

    private void ScanCIRTargets(TypeRhs tpRhs) {
        if (tpRhs.ArrayDimensions == null)
            return;
        
        var type = tpRhs.EType switch {
            IntType => "int",
            RealType => "real",
            BitvectorType => "bv",
            BoolType => "bool",
            CharType => "char",
            UserDefinedType uType => uType.Name == "string" ? "string" : "",
            _ => "",
        };
        var exprLocation = $"{tpRhs.StartToken.pos}-{tpRhs.EndToken.pos}";
        var hasInit = (tpRhs.InitDisplay != null && tpRhs.InitDisplay.Count != 0) || tpRhs.ElementInit != null;
        if (!hasInit && type == "")
            return;
        var arg = hasInit ? "" : type;
        Targets.Add((exprLocation, "CIR", arg));
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
        ScanUOITargets(litExpr);
        ScanLVRTargets(litExpr);
    }
    protected override void VisitExpression(BinaryExpr bExpr) {
        ScanUOITargets(bExpr);
        ScanEVRTargets(bExpr);
        base.VisitExpression(bExpr);
    }
    
    protected override void VisitExpression(UnaryExpr uExpr) {
        _skipChildUOIMutation = true;
        ScanEVRTargets(uExpr);
        base.VisitExpression(uExpr);
    }
    
    protected override void VisitExpression(ParensExpression pExpr) {
        ScanUOITargets(pExpr);
        _skipChildUOIMutation = true;
        base.VisitExpression(pExpr);
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        _skipChildUOIMutation = true;
        ScanEVRTargets(nExpr);
        base.VisitExpression(nExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        ScanUOITargets(cExpr);
        ScanEVRTargets(cExpr);
        foreach (var operand in cExpr.Operands) {
            if (operand is not NegationExpression)
                ScanUOITargets(operand);
        }
    }

    protected override void VisitExpression(NameSegment nSegExpr) {
        ScanUOITargets(nSegExpr);
        ScanEVRTargets(nSegExpr);
    }
    
    protected override void VisitExpression(LetExpr ltExpr) {
        ScanUOITargets(ltExpr);
        ScanEVRTargets(ltExpr);
        base.VisitExpression(ltExpr);
    }
    
    protected override void VisitExpression(LetOrFailExpr ltOrFExpr) {
        ScanUOITargets(ltOrFExpr);
        ScanEVRTargets(ltOrFExpr);
        base.VisitExpression(ltOrFExpr);
    }
    
    protected override void VisitExpression(ApplyExpr appExpr) {
        ScanUOITargets(appExpr);
        ScanEVRTargets(appExpr);
        base.VisitExpression(appExpr);
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        ScanUOITargets(suffixExpr);
        ScanEVRTargets(suffixExpr);
        _skipChildEVRMutation = true;
        base.VisitExpression(suffixExpr);
        _skipChildEVRMutation = false;
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        ScanUOITargets(fCallExpr);
        ScanEVRTargets(fCallExpr);
        base.VisitExpression(fCallExpr);
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        ScanUOITargets(mSelExpr);
        ScanEVRTargets(mSelExpr);
        base.VisitExpression(mSelExpr);
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        ScanUOITargets(iteExpr);
        ScanEVRTargets(iteExpr);
        base.VisitExpression(iteExpr);
    }
    
    protected override void VisitExpression(MatchExpr mExpr) {
        ScanUOITargets(mExpr);
        ScanEVRTargets(mExpr);
        base.VisitExpression(mExpr);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        ScanUOITargets(nMExpr);
        ScanEVRTargets(nMExpr);
        base.VisitExpression(nMExpr);
    }
    
    protected override void VisitExpression(DisplayExpression dExpr) {
        ScanUOITargets(dExpr);
        ScanCIRTargets(dExpr);
        base.VisitExpression(dExpr);
    }
    
    protected override void VisitExpression(MapDisplayExpr mDExpr) {
        ScanUOITargets(mDExpr);
        ScanCIRTargets(mDExpr);
        base.VisitExpression(mDExpr);
    }
    
    protected override void VisitExpression(SeqConstructionExpr seqCExpr) {
        ScanUOITargets(seqCExpr);
        ScanEVRTargets(seqCExpr);
        base.VisitExpression(seqCExpr);
    }
    
    protected override void VisitExpression(MultiSetFormingExpr mSetFExpr) {
        ScanUOITargets(mSetFExpr);
        ScanEVRTargets(mSetFExpr);
        base.VisitExpression(mSetFExpr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        ScanUOITargets(seqSExpr);
        ScanEVRTargets(seqSExpr);
        _skipChildEVRMutation = true;
        base.VisitExpression(seqSExpr);
        _skipChildEVRMutation = false;
    }
    
    protected override void VisitExpression(MultiSelectExpr mSExpr) {
        ScanUOITargets(mSExpr);
        ScanEVRTargets(mSExpr);
        _skipChildEVRMutation = true;
        base.VisitExpression(mSExpr);
        _skipChildEVRMutation = false;
    }
    
    protected override void VisitExpression(SeqUpdateExpr seqUExpr) {
        ScanUOITargets(seqUExpr);
        ScanEVRTargets(seqUExpr);
        base.VisitExpression(seqUExpr);
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        ScanUOITargets(compExpr);
        ScanEVRTargets(compExpr);
        base.VisitExpression(compExpr);
    }
    
    protected override void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        ScanUOITargets(dtUExpr);
        ScanEVRTargets(dtUExpr);
        base.VisitExpression(dtUExpr);
    }
    
    protected override void VisitExpression(DatatypeValue dtValue) {
        ScanUOITargets(dtValue);
        ScanEVRTargets(dtValue);
        base.VisitExpression(dtValue);
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        ScanUOITargets(stmtExpr);
        ScanEVRTargets(stmtExpr);
        base.VisitExpression(stmtExpr);
    }
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    protected override void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
        } else if (aRhs is TypeRhs tpRhs) {
            ScanCIRTargets(tpRhs);
            
            var elInit = tpRhs.ElementInit;
            if (tpRhs.ArrayDimensions != null) {
                HandleExprList(tpRhs.ArrayDimensions);
            } if (elInit != null && IsWorthVisiting(elInit.StartToken.pos, elInit.EndToken.pos)) {
                HandleExpression(elInit);
            } if (tpRhs.InitDisplay != null) {
                HandleExprList(tpRhs.InitDisplay);
            } if (tpRhs.Bindings != null) {
                HandleActualBindings(tpRhs.Bindings);
            }
        }
    }
}