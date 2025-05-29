using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Visitor;

public class PostResolveTargetScanner(List<string> operatorsInUse, ErrorReporter reporter) : TargetScanner(operatorsInUse, reporter)
{
    private bool _skipChildUOIMutation;
    private bool _skipChildEVRMutation;
    private bool _skipChildVERMutation;
    private bool _skipChildDCRMutation;
    private bool _skipChildFARMutation;
    private bool _typesInferredFromContext;
    private string _childMethodCallPos = "";
    private ExprDotName? _childExprDotName;
    private List<string> _childMethodCallArgTypes = [];
    private Dictionary<string, Type> _currentScopeVars = [];
    private readonly List<MethodOrFunction> _declaredMethods = [];
    private readonly List<ApplySuffix> _methodCalls = [];
    private readonly List<(string, ClassLikeDecl, Type)> _classFields = []; // field name, class type, field type
    private readonly List<ExprDotName> _accessedClassFields = [];
    private Dictionary<string, Type> _currentScopeChildClassVariables = [];
    private List<(string, int, Type)> _childClassAccessedVariables = [];
    
    private void ScanUOITargets(Expression expr) {
        if (!ShouldImplement("UOI")) return;
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
        if (!ShouldImplement("LVR")) return;
        
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
        if (!ShouldImplement("EVR")) return;
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
    
    private void ScanEVRTargets(TypeRhs tpRhs) {
        if (!ShouldImplement("EVR")) return;
        if (_skipChildEVRMutation) return;
        
        if (tpRhs.Type is UserDefinedType uType && uType.ResolvedClass is NonNullTypeDecl nnTypeDecl && 
            nnTypeDecl.Class != null) {
            Targets.Add(($"{tpRhs.StartToken.pos}-{tpRhs.EndToken.pos}", "EVR", "null"));
        }
    }
    
    private void ScanVERTargets(NameSegment nSegExpr) {
        if (!ShouldImplement("VER")) return;

        foreach (var var in _currentScopeVars) {
            if (nSegExpr.Name == var.Key) continue;
            if (nSegExpr.Type.ToString() == var.Value.ToString())
                Targets.Add(($"{nSegExpr.Center.pos}", "VER", var.Key));
        }
    }
    
    private void ScanCIRTargets(Expression expr) {
        if (!ShouldImplement("CIR")) return;
        
        var exprLocation = $"{expr.StartToken.pos}-{expr.EndToken.pos}";
        string type, arg;
        switch (expr) {
            case DisplayExpression dExpr:
                type = PrimitiveTypeToStr(dExpr.Type.TypeArgs[0]);
                if (dExpr.Elements.Count == 0 && type == "") 
                    return;
                arg = dExpr.Elements.Count == 0 ? type : "";
                Targets.Add((exprLocation, "CIR", arg)); break;
            case MapDisplayExpr mDExpr:
                type = $"{PrimitiveTypeToStr(mDExpr.Type.TypeArgs[0])}-{PrimitiveTypeToStr(mDExpr.Type.TypeArgs[1])}";
                if (mDExpr.Elements.Count == 0 && type == "") 
                    return;
                arg = mDExpr.Elements.Count == 0 ? type : "";
                Targets.Add((exprLocation, "CIR", arg)); break;
        }
    }

    private void ScanCIRTargets(TypeRhs tpRhs) {
        if (!ShouldImplement("CIR")) return;
        if (tpRhs.ArrayDimensions == null) return;

        var type = PrimitiveTypeToStr(tpRhs.EType);
        var exprLocation = $"{tpRhs.StartToken.pos}-{tpRhs.EndToken.pos}";
        var hasInit = (tpRhs.InitDisplay != null && tpRhs.InitDisplay.Count != 0) || tpRhs.ElementInit != null;
        if (!hasInit && type == "")
            return;
        var arg = hasInit ? "" : type;
        Targets.Add((exprLocation, "CIR", arg));
    }

    private void ScanMCRTargets() {
        if (!ShouldImplement("MCR")) return;
        
        foreach (var methodCall in _methodCalls) {
            if (methodCall.Lhs is not NameSegment nSegExpr) continue;
            var methodName = nSegExpr.Name;
            var method = _declaredMethods.First(m => m.Name == methodName);

            foreach (var m in _declaredMethods) {
                if (m == method) continue;
                
                var m1InTypes = method.Ins.Select(o => o.Type.ToString()).ToList();
                var m2InTypes = m.Ins.Select(o => o.Type.ToString()).ToList();
                if (!m1InTypes.SequenceEqual(m2InTypes)) continue;
                
                if (method is Method m1 && m is Method m2) { // functions don't support a list of outputs
                    var m1OutTypes = m1.Outs.Select(o => o.Type.ToString()).ToList();
                    var m2OutTypes = m2.Outs.Select(o => o.Type.ToString()).ToList();
                    if (!m1OutTypes.SequenceEqual(m2OutTypes)) continue;
                } else if (method is Function f1 && m is Function f2) {
                    if (f1.ResultType.ToString() != f2.ResultType.ToString()) continue;
                } else {
                    continue;
                }
                
                Targets.Add(($"{methodCall.Center.pos}", "MCR", $"{m.Name}"));
            }
        }
    }

    private void ScanMCRTargets(ConcreteAssignStatement cAStmt) {
        if (!ShouldImplement("MCR")) return;
        var typeArg = "";
        
        // default replacement
        foreach (var lhs in cAStmt.Lhss) {
            var type = TypeToStr(lhs.Type);
            if (type == "") {
                typeArg = "";
                break;
            }
            typeArg = typeArg == "" ? type : $"{typeArg}-{type}";
        }
        if (typeArg != "")
            Targets.Add((_childMethodCallPos, "MCR", typeArg));
        
        ScanArgPropagationTargets(cAStmt);
        ScanNakedReceiverTargets(cAStmt);
    }

    private void ScanArgPropagationTargets(ConcreteAssignStatement cAStmt) {
        var argProp = "";
        foreach (var lhs in cAStmt.Lhss) {
            var type = TypeToStr(lhs.Type);
            var methodArgPos = _childMethodCallArgTypes.IndexOf(type);
            if (methodArgPos == -1) {
                argProp = "";
                break;
            }
            argProp = argProp == "" ? $"{methodArgPos}" : $"{argProp}-{methodArgPos}";
        }
        if (argProp != "")
            Targets.Add((_childMethodCallPos, "MCR", argProp));
    }

    private void ScanNakedReceiverTargets(ConcreteAssignStatement cAStmt) {
        if (_childExprDotName == null) return;
        if (cAStmt.Lhss.Count == 1 && // naked receiver can be applied if the types match or if they are inferred from context
            (TypeToStr(cAStmt.Lhss[0].Type) == TypeToStr(_childExprDotName.Lhs.Type) || _typesInferredFromContext))
            Targets.Add((_childMethodCallPos, "MCR", ""));
    }

    private void ScanSARTargets(ApplySuffix appSufExpr) {
        if (!ShouldImplement("SAR")) return;

        for (var i = 0; i < appSufExpr.Bindings.ArgumentBindings.Count; i++) {
            for (var j = i + 1; j < appSufExpr.Bindings.ArgumentBindings.Count; j++) {
                var b1 = appSufExpr.Bindings.ArgumentBindings[i];
                var b2 = appSufExpr.Bindings.ArgumentBindings[j];
                if (b1.Actual.Type.ToString() != b2.Actual.Type.ToString()) 
                    continue;
                Targets.Add(($"{b1.Actual.Center.pos}", "SAR", $"{b2.Actual.Center.pos}"));
            }
        }
    }

    private void ScanDCRTargets(ApplySuffix appSufExpr) {
        if (!ShouldImplement("DCR") || appSufExpr.Type.AsDatatype == null) return;
        
        if (appSufExpr.Lhs is not NameSegment nSegExpr) return;
        var dtCtor = nSegExpr.Name;
        var numArgs = appSufExpr.Bindings.ArgumentBindings.Count;
        var argTypes = appSufExpr.Bindings.ArgumentBindings.Select(a => a.Actual.Type).ToList();
        
        foreach (var ctor in appSufExpr.Type.AsDatatype.Ctors) {
            if (ctor.Name == dtCtor || ctor.Formals.Count != numArgs) continue;
            var signatureMatches = true;
            foreach (var (formal, i) in ctor.Formals.Select((f, i) => (f, i))) {
                if (formal.Type.ToString() == argTypes[i].ToString()) continue;
                signatureMatches = false;
                break;
            }
            
            if (signatureMatches)
                Targets.Add(($"{appSufExpr.Center.pos}", "DCR", $"{ctor.Name}"));
        }
    }
    
    private void ScanDCRTargets(NameSegment nSegExpr) {
        if (!ShouldImplement("DCR") || _skipChildDCRMutation || nSegExpr.Type.AsDatatype == null) 
            return;
        
        var dtCtor = nSegExpr.Name;
        foreach (var ctor in nSegExpr.Type.AsDatatype.Ctors) {
            if (ctor.Name != dtCtor && ctor.Formals.Count == 0)
                Targets.Add(($"{nSegExpr.Center.pos}", "DCR", $"{ctor.Name}"));
        }
    }

    private void ScanPRVTargets() {
        if (!ShouldImplement("PRV")) return;
        
        foreach (var childClassVar1 in _childClassAccessedVariables) {
            foreach (var childClassVar2 in _currentScopeChildClassVariables) {
                if (childClassVar1.Item1 == childClassVar2.Key) continue;
                
                var var1Class = childClassVar1.Item3.AsTopLevelTypeWithMembers.Name;
                var var1Parents = childClassVar1.Item3.AsTopLevelTypeWithMembers.ParentTraitHeads;
                var var2Class = childClassVar2.Value.AsTopLevelTypeWithMembers.Name;
                var var2Parents = childClassVar2.Value.AsTopLevelTypeWithMembers.ParentTraitHeads;
                if (var1Parents.All(var2Parents.Contains) && 
                    var1Parents.Count == var2Parents.Count && 
                    var1Class != var2Class)
                {
                    Targets.Add(($"{childClassVar1.Item2}", "PRV", childClassVar2.Key));
                }
            }
        }
    }

    private void ScanFARMutations() {
        if (!ShouldImplement("FAR")) return;

        foreach (var fieldAccess in _accessedClassFields) {
            var classDecl1 = fieldAccess.Lhs.Type?.AsTopLevelTypeWithMembers;
            if (classDecl1 == null) continue;
            var parents = classDecl1.ParentTraitHeads.Select(t => t.ToString()).ToList();
            
            foreach (var field in _classFields) {
                if (field.Item1 == fieldAccess.SuffixName) continue;
                var classDecl2 = field.Item2;
                
                if ((classDecl1.ToString() == classDecl2.ToString() || parents.Contains(classDecl2.ToString())) &&
                    fieldAccess.Type.ToString() == field.Item3.ToString())
                    Targets.Add(($"{fieldAccess.Center.pos}", "FAR", field.Item1));
            }
        }
    }
    
    private string PrimitiveTypeToStr(Type type) {
        if (type.IsIntegerType) return "int";
        if (type.IsRealType) return "real";
        if (type.IsBitVectorType) return "bv";
        if (type.IsBoolType) return "bool";
        if (type.IsCharType) return "char";
        return type.IsStringType ? "string" : "";
    }
    
    private string TypeToStr(Type type) {
        return type switch {
            IntType => "int",
            RealType => "real",
            BitvectorType => "bv",
            BoolType => "bool",
            CharType => "char", 
            SetType => "set", 
            MultiSetType => "multiset",
            SeqType => "seq",
            MapType => "map",
            UserDefinedType uType => uType.Name == "string" ? "string" :  "",
            _ => "",
        };
    }
    
    /// -------------------------------------
    /// Group of overriden top level visitors
    /// -------------------------------------
    public override void Find(ModuleDefinition module) {
        base.Find(module);
        ScanMCRTargets();
        ScanFARMutations();
    }
    
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (decl is ClassLikeDecl clDecl && member is Field f)
                _classFields.Add((f.Name, clDecl, f.Type));
            
            if (member is not ConstantField cf || cf.Rhs == null) 
                continue;
            
            if (ShouldImplement("SDL")) {
                var fieldType = TypeToStr(cf.Type);
                if (fieldType != "")
                    Targets.Add(($"{cf.Center.pos}", "SDL", ""));
            }
            
            if (!_currentScopeVars.ContainsKey(cf.Name)) 
                _currentScopeVars.Add(cf.Name, cf.Type);
            var classDecl = cf.Type.AsTopLevelTypeWithMembers;
            if (classDecl != null && classDecl is ClassDecl &&
                classDecl.ParentTraitHeads.Count > 0 &&
                !_currentScopeChildClassVariables.ContainsKey(cf.Name)) 
            {
                _currentScopeChildClassVariables.Add(cf.Name, cf.Type);
            }
        }
        base.HandleMemberDecls(decl);
        ScanPRVTargets();
        _childClassAccessedVariables = [];
    }
    
    protected override void HandleMethod(Method method) {
        _declaredMethods.Add(method);
        
        var methodIndependentVars = new Dictionary<string, Type>(_currentScopeVars);
        var methodIndependentChildClassVars = new Dictionary<string, Type>(_currentScopeChildClassVariables);
        foreach (var formal in method.Ins) {
            if (!_currentScopeVars.ContainsKey(formal.Name))
                _currentScopeVars.Add(formal.Name, formal.Type);
            
            var classDecl = formal.Type.AsTopLevelTypeWithMembers;
            if (classDecl != null && classDecl is ClassDecl && 
                classDecl.ParentTraitHeads.Count > 0 && 
                !_currentScopeChildClassVariables.ContainsKey(formal.Name))
            {
                _currentScopeChildClassVariables.Add(formal.Name, formal.Type);
            }
        }
        base.HandleMethod(method);
        ScanPRVTargets();
        _currentScopeVars = methodIndependentVars;
        _currentScopeChildClassVariables = methodIndependentChildClassVars;
        _childClassAccessedVariables = [];
    }
    
    protected override void HandleFunction(Function function) {
        _declaredMethods.Add(function);
        base.HandleFunction(function);
    }

    /// -------------------------------------
    /// Group of overriden statement visitors
    /// -------------------------------------
    protected override void HandleBlock(List<Statement> statements) {
        var blockIndependentVars = new Dictionary<string, Type>(_currentScopeVars);
        var blockIndependentChildClassVars = new Dictionary<string, Type>(_currentScopeChildClassVariables);
        base.HandleBlock(statements);
        ScanPRVTargets();
        _currentScopeVars = blockIndependentVars;
        _currentScopeChildClassVariables = blockIndependentChildClassVars;
        _childClassAccessedVariables = [];
    }
    
    protected override void VisitStatement(ConcreteAssignStatement cAStmt) { }

    private void VisitLhss(ConcreteAssignStatement cAStmt) {
        foreach (var lhs in cAStmt.Lhss) {
            if (lhs is NameSegment nSegExpr && !_currentScopeVars.ContainsKey(nSegExpr.Name))
                _currentScopeVars.Add(nSegExpr.Name, nSegExpr.Type);
        }
    }
    
    protected override void VisitStatement(AssignStatement aStmt) {
        base.VisitStatement(aStmt);
        VisitLhss(aStmt);
        
        if (_childMethodCallPos != "") // rhs is method call
            ScanMCRTargets(aStmt);
        _childMethodCallPos = "";
        _childMethodCallArgTypes = [];
        _childExprDotName = null;
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        base.VisitStatement(aStStmt);
        VisitLhss(aStStmt);

        if (_childMethodCallPos != "") // rhs is method call
            ScanMCRTargets(aStStmt);
        _childMethodCallPos = "";
        _childMethodCallArgTypes = [];
        _childExprDotName = null;
    }
    
    protected override void VisitStatement(SingleAssignStmt sAStmt) {
        HandleRhsList([sAStmt.Rhs]);
    }

    protected override void VisitStatement(VarDeclStmt vDeclStmt) {
        if (vDeclStmt.IsGhost) return;
        _typesInferredFromContext = vDeclStmt.Locals[0].SafeSyntacticType is InferredTypeProxy;
        base.VisitStatement(vDeclStmt);
        
        foreach (var var in vDeclStmt.Locals) {
            if (!_currentScopeVars.ContainsKey(var.Name))
                _currentScopeVars.Add(var.Name, var.Type);
            
            var classDecl = var.Type.AsTopLevelTypeWithMembers;
            if (classDecl != null && classDecl is ClassDecl && 
                classDecl.ParentTraitHeads.Count > 0 && 
                !_currentScopeChildClassVariables.ContainsKey(var.Name))
            {
                _currentScopeChildClassVariables.Add(var.Name, var.Type);
            }
        }
        _typesInferredFromContext = false;
    }

    /// --------------------------------------
    /// Group of overriden expression visitors
    /// --------------------------------------
    protected override void VisitExpression(LiteralExpr litExpr) {
        if (!((litExpr.Value is BigInteger bi && bi == BigInteger.Zero) || 
              (litExpr.Value is BigDec bd && bd != BigDec.ZERO))) {
            ScanUOITargets(litExpr);
        }
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
        base.VisitExpression(nExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        ScanUOITargets(cExpr);
        ScanEVRTargets(cExpr);
        foreach (var operand in cExpr.Operands) {
            if (operand is not NegationExpression)
                ScanUOITargets(operand);
            
            if (operand is LiteralExpr litExpr) {
                ScanLVRTargets(litExpr);
            } else if (operand is not NegationExpression) {
                ScanEVRTargets(operand);
            }
            
            if (operand is NameSegment nSegExpr)
                ScanVERTargets(nSegExpr);
            if (operand is SuffixExpr suffixExpr && !_skipChildFARMutation && 
                suffixExpr is ExprDotName exprDName && exprDName.Lhs is NameSegment nSegExprLhs && 
                nSegExprLhs.Type.AsTopLevelTypeWithMembers != null && nSegExprLhs.Type.AsTopLevelTypeWithMembers is ClassLikeDecl)
            {
                _accessedClassFields.Add(exprDName);
            }
        }
    }

    protected override void VisitExpression(NameSegment nSegExpr) {
        if (!_skipChildVERMutation)
            ScanVERTargets(nSegExpr);
        
        var classDecl = nSegExpr.Type.AsTopLevelTypeWithMembers;
        if (classDecl != null && classDecl is ClassDecl && 
            classDecl.ParentTraitHeads.Count > 0 && 
            !_childClassAccessedVariables.Contains((nSegExpr.Name, nSegExpr.Center.pos, nSegExpr.Type)))
        {
            _childClassAccessedVariables.Add((nSegExpr.Name, nSegExpr.Center.pos, nSegExpr.Type));
        }
        
        ScanUOITargets(nSegExpr);
        ScanEVRTargets(nSegExpr);
        ScanDCRTargets(nSegExpr);
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
        _childMethodCallPos = $"{suffixExpr.Center.pos}";
        if (suffixExpr is ApplySuffix appSufExpr) {
            if (appSufExpr.Lhs is NameSegment)
                _methodCalls.Add(appSufExpr);
            if (appSufExpr.Type != null && appSufExpr.Type.IsDatatype)
                ScanDCRTargets(appSufExpr);
            foreach (var binding in appSufExpr.Bindings.ArgumentBindings) {
                _childMethodCallArgTypes.Add(TypeToStr(binding.Actual.Type));
            }
            ScanSARTargets(appSufExpr);

            _skipChildFARMutation = true;
        }
        if (suffixExpr.Lhs is ExprDotName exprDNameLhs)
            _childExprDotName = exprDNameLhs;

        if (!_skipChildFARMutation && suffixExpr is ExprDotName exprDName && exprDName.Lhs is NameSegment nSegExpr && 
            nSegExpr.Type.AsTopLevelTypeWithMembers != null && nSegExpr.Type.AsTopLevelTypeWithMembers is ClassLikeDecl)
        {
            _accessedClassFields.Add(exprDName);
        }
        
        ScanUOITargets(suffixExpr);
        ScanEVRTargets(suffixExpr);
        _skipChildEVRMutation = true;
        _skipChildVERMutation = true;
        _skipChildDCRMutation = true;
        base.VisitExpression(suffixExpr);
        _skipChildEVRMutation = false;
        _skipChildVERMutation = false;
        _skipChildDCRMutation = false;
        _skipChildFARMutation = false;
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
            ScanEVRTargets(tpRhs);
            
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