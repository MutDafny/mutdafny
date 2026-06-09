using System.Numerics;
using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class PreResolveTargetScanner(string mutationTargetURI, string mutationTargetMethod, int mutationTargetLine, (int, int) mutationTargetRange, List<string> operatorsInUse, ErrorReporter reporter)
    : TargetScanner(mutationTargetURI, mutationTargetLine, mutationTargetRange, operatorsInUse, reporter)
{
    private List<string> _coveredVariableNames = [];
    private List<string> _prevCoveredVariableNames = [];
    private List<string> _varsToDelete = [];
    private static readonly List<BinaryExpr.Opcode> CoveredOperators = [];
    private (Token?, Token?) _currentScope;
    private List<string> _currentMethodIns = [];
    private List<string> _currentMethodOuts = [];
    private List<string> _currentInitMethodOuts = [];
    private List<string> _currentSourceDeclFields = [];
    private Dictionary<string, Expression> _assigns = [];
    private List<Expression> _loopBoundVarUpdates = [];
    private Statement? _parentLoopGuardBody;
    private NameSegment? _currentLoopBoundVar;
    private bool _visitFurther = true;
    private bool _isCurrentMethodVoid;
    private bool _parentBlockHasStmt;
    private bool _isChildIfBlock;
    private bool _isParentVarDeclStmt;
    private bool _scanThisKeywordTargets;
    
    private readonly Dictionary<BinaryExpr.Opcode, List<BinaryExpr.Opcode>> _replacementList = new()
    {
        // arithmetic operators
        { BinaryExpr.Opcode.Add, [BinaryExpr.Opcode.Sub, BinaryExpr.Opcode.Mul] },
        { BinaryExpr.Opcode.Sub, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Mul] },
        { BinaryExpr.Opcode.Mul, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Sub] },
        { BinaryExpr.Opcode.Div, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Sub, BinaryExpr.Opcode.Mul, BinaryExpr.Opcode.Mod] },
        { BinaryExpr.Opcode.Mod, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Sub, BinaryExpr.Opcode.Mul, BinaryExpr.Opcode.Div] },
        // relational operators
        { BinaryExpr.Opcode.Eq, [BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge] },
        { BinaryExpr.Opcode.Neq, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge] },
        { BinaryExpr.Opcode.Lt, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge] },
        { BinaryExpr.Opcode.Le, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge] },
        { BinaryExpr.Opcode.Gt, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Ge] },
        { BinaryExpr.Opcode.Ge, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt] },
        // conditional operators
        { BinaryExpr.Opcode.And, [BinaryExpr.Opcode.Or, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
        { BinaryExpr.Opcode.Or, [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
        { BinaryExpr.Opcode.Iff, [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Or, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
        { BinaryExpr.Opcode.Imp, [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Or, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Exp] },
        { BinaryExpr.Opcode.Exp, [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Or, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp] },
        // logical operators
        { BinaryExpr.Opcode.BitwiseAnd, [BinaryExpr.Opcode.BitwiseOr, BinaryExpr.Opcode.BitwiseXor] },
        { BinaryExpr.Opcode.BitwiseOr, [BinaryExpr.Opcode.BitwiseAnd, BinaryExpr.Opcode.BitwiseXor] },
        { BinaryExpr.Opcode.BitwiseXor, [BinaryExpr.Opcode.BitwiseAnd, BinaryExpr.Opcode.BitwiseOr] },
        // shift operators
        { BinaryExpr.Opcode.LeftShift, [BinaryExpr.Opcode.RightShift] },
        { BinaryExpr.Opcode.RightShift, [BinaryExpr.Opcode.LeftShift] },
    };
    private readonly Dictionary<BinaryExpr.Opcode, string> _replacementOpType = new()
    {
        // arithmetic operators
        { BinaryExpr.Opcode.Add, "AOR" }, { BinaryExpr.Opcode.Sub, "AOR" },
        { BinaryExpr.Opcode.Mul, "AOR" }, { BinaryExpr.Opcode.Div, "AOR" }, { BinaryExpr.Opcode.Mod, "AOR" },
        // relational operators
        { BinaryExpr.Opcode.Eq, "ROR" }, { BinaryExpr.Opcode.Neq, "ROR" },
        { BinaryExpr.Opcode.Lt, "ROR" }, { BinaryExpr.Opcode.Le, "ROR" },
        { BinaryExpr.Opcode.Gt, "ROR" }, { BinaryExpr.Opcode.Ge, "ROR" },
        // conditional operators
        { BinaryExpr.Opcode.And, "COR" }, { BinaryExpr.Opcode.Or, "COR" },
        { BinaryExpr.Opcode.Iff, "COR" }, { BinaryExpr.Opcode.Imp, "COR" }, { BinaryExpr.Opcode.Exp, "COR" },
        // logical operators
        { BinaryExpr.Opcode.BitwiseAnd, "LOR" }, { BinaryExpr.Opcode.BitwiseOr, "LOR" }, { BinaryExpr.Opcode.BitwiseXor, "LOR" },
        // shift operators
        { BinaryExpr.Opcode.LeftShift, "SOR" }, { BinaryExpr.Opcode.RightShift, "SOR" },
    };

    private void ScanThisKeywordTargets(TopLevelDeclWithMembers decl) {
        if (!ShouldImplement("THI") && !ShouldImplement("THD")) return;
        foreach (var member in decl.Members) {
            if (member.IsGhost) continue; // only searches for mutation targets in non-ghost constructs

            if (member is MethodOrFunction mf) {
                if (mutationTargetMethod != "" && mf.Name != mutationTargetMethod) continue;
                
                _currentMethodIns = mf.Ins.Select(i => i.Name).ToList();
                if (!_currentMethodIns.Intersect(_currentSourceDeclFields).Any())
                    continue;
                _scanThisKeywordTargets = true;
                if (!_scanThisKeywordTargets) continue;
            }
            if (member is Method m) {
                HandleMethod(m);
            } else if (member is Function f) {
                HandleFunction(f);
            }
        }
        
        _currentMethodIns = [];
    }

    private void ScanMBRTargets(TopLevelDeclWithMembers decl) {
        if (!ShouldImplement("AMR") && !ShouldImplement("MMR")) return;
        if (mutationTargetMethod != "") return;
        
        var getters = new List<Method>();
        var setters = new List<Method>();
        foreach (var member in decl.Members) {
            if (member is not Method m) continue;
            if (IsGetter(m))
                getters.Add(m);
            if (IsSetter(m))
                setters.Add(m);
        }

        if (ShouldImplement("AMR")) 
            ComputeMethodReplacements(getters, true);
        if (!ShouldImplement("MMR")) return;
        ComputeMethodReplacements(setters, false);
    }

    private void ComputeMethodReplacements(List<Method> methods, bool isGetter) {
        for (var i = 0; i < methods.Count; i++) {
            for (var j = i+1; j < methods.Count; j++) {
                var m1 = methods[i];
                var m2 = methods[j];
                
                if (isGetter) {
                    var m1OutTypes = m1.Outs.Select(o => o.Type.ToString()).ToList();
                    var m2OutTypes = m2.Outs.Select(o => o.Type.ToString()).ToList();
                    if (m1OutTypes.SequenceEqual(m2OutTypes))
                        AddTarget(($"{m1.StartToken.pos}-{m1.EndToken.pos}", "AMR", $"{m2.StartToken.pos}-{m2.EndToken.pos}"));
                } else {
                    var m1InTypes = m1.Ins.Select(f => f.Type.ToString()).ToList();
                    var m2InTypes = m2.Ins.Select(f => f.Type.ToString()).ToList();
                    if (m1InTypes.SequenceEqual(m2InTypes))
                        AddTarget(($"{m1.StartToken.pos}-{m1.EndToken.pos}", "MMR", $"{m2.StartToken.pos}-{m2.EndToken.pos}"));
                }
            }
        }
    }

    private List<string> GetLhsNameList(List<Expression> lhss) {
        var lhsNameList = new List<string>();
        foreach (var lhs in lhss) {
            if (lhs is NameSegment nSegExpr) {
                lhsNameList.Add(nSegExpr.Name);
            } else if (lhs is ExprDotName exprDName && exprDName.Lhs is ThisExpr) {
                lhsNameList.Add(exprDName.SuffixName);
            } else {
                return [];
            }
        }
        return lhsNameList;
    }

    private List<string> GetRhsNameList(List<AssignmentRhs> rhss) {
        var rhsNameList = new List<string>();
        foreach (var rhs in rhss) {
            if (rhs is not ExprRhs exprRhs) return [];
            if (exprRhs.Expr is NameSegment nSegExpr) {
                rhsNameList.Add(nSegExpr.Name);
                continue;
            }
            if (exprRhs.Expr is ExprDotName exprDName && exprDName.Lhs is ThisExpr) {
                rhsNameList.Add(exprDName.SuffixName);
                continue;
            }
            return [];
        }

        return rhsNameList;
    }
    
    private bool IsGetter(Method method) {
        if (method.Ins.Count != 0 || method.Body == null || method.Body.Body.Count == 0) 
            return false;
        
        foreach (var stmt in method.Body.Body) {
            if (stmt is ReturnStmt rStmt) { // all rhs are fields
                var rhsNames = GetRhsNameList(rStmt.Rhss);
                if (rhsNames.Count == 0) return false;
                if (rhsNames.Any(rhs => !_currentSourceDeclFields.Contains(rhs)))
                    return false;
            } 
            if (stmt is ConcreteAssignStatement cAStmt) { // all lhs are return vars and all rhs are fields
                if (cAStmt.Lhss.Any(lhs => lhs is not NameSegment))
                    return false;
                var lhsNames = cAStmt.Lhss.Select(lhs => (lhs as NameSegment)?.Name).ToList();
                var outNames = method.Outs.Select(o => o.Name).ToList();
                if (lhsNames.Any(lhs => lhs != null && !outNames.Contains(lhs)))
                    return false;
                
                List<string> rhsNames = new List<string>();
                if (cAStmt is AssignSuchThatStmt aStStmt) {
                    if (aStStmt.Expr is not NameSegment nSegExpr) return false;
                    rhsNames = [nSegExpr.Name];
                } else if (cAStmt is AssignStatement aStmt) {
                    rhsNames = GetRhsNameList(aStmt.Rhss);
                } else if (cAStmt is AssignOrReturnStmt aOrRStmt) {
                    rhsNames = GetRhsNameList(aOrRStmt.Rhss);
                }
                if (rhsNames.Count == 0) return false;
                if (rhsNames.Any(rhs => !_currentSourceDeclFields.Contains(rhs)))
                    return false;
            } else {
                return false;
            }
        }
        return true;
    }

    private bool IsSetter(Method method) {
        if (method.Outs.Count != 0 || method.Body == null || method.Body.Body.Count == 0) 
            return false;

        foreach (var stmt in method.Body.Body) {
            if (stmt is ConcreteAssignStatement cAStmt) {
                var lhsNames = GetLhsNameList(cAStmt.Lhss);
                if (lhsNames.Count == 0) return false;
                if (lhsNames.Any(lhs => !_currentSourceDeclFields.Contains(lhs)))
                    return false;
            } else {
                return false;
            }
        }
        return true;
    }

    private bool ExcludeBORTarget(BinaryExpr bExpr, BinaryExpr.Opcode replacementOp) {
        // a.Length == 0 <==> a.Length <= 0
        if ((bExpr.E0 is UnaryOpExpr uOpExpr1 && uOpExpr1.Op is UnaryOpExpr.Opcode.Cardinality ||
             bExpr.E0 is ExprDotName exprDName1 && exprDName1.SuffixName == "Length") &&
            bExpr.E1 is LiteralExpr litExpr1 && litExpr1.Value is BigInteger bi1 && bi1 == BigInteger.Zero &&
            ((bExpr.Op is BinaryExpr.Opcode.Eq && replacementOp is BinaryExpr.Opcode.Le) ||
             (bExpr.Op is BinaryExpr.Opcode.Le && replacementOp is BinaryExpr.Opcode.Eq)))
            return true;
        // 0 == a.Length <==> 0 >= a.Length
        if ((bExpr.E1 is UnaryOpExpr uOpExpr2 && uOpExpr2.Op is UnaryOpExpr.Opcode.Cardinality ||
             bExpr.E1 is ExprDotName exprDName2 && exprDName2.SuffixName == "Length") &&
            bExpr.E1 is LiteralExpr litExpr2 && litExpr2.Value is BigInteger bi2 && bi2 == BigInteger.Zero &&
            ((bExpr.Op is BinaryExpr.Opcode.Eq && replacementOp is BinaryExpr.Opcode.Ge) ||
             (bExpr.Op is BinaryExpr.Opcode.Ge && replacementOp is BinaryExpr.Opcode.Eq)))
            return true;
        
        if (IsEquivalentRORLoopGuardTarget(bExpr)) {
            // a.Length > n <==> a.Length != n; in a loop guard if n is initially 0 and incremented 1 at a time
            if ((bExpr.E0 is UnaryOpExpr uOpExpr3 && uOpExpr3.Op is UnaryOpExpr.Opcode.Cardinality ||
                 bExpr.E0 is ExprDotName exprDName3 && exprDName3.SuffixName == "Length") &&
                ((bExpr.Op is BinaryExpr.Opcode.Gt && replacementOp is BinaryExpr.Opcode.Neq) ||
                 (bExpr.Op is BinaryExpr.Opcode.Neq && replacementOp is BinaryExpr.Opcode.Gt)))
                return true;
            // n < a.Length <==> n != a.Length; in a loop guard if n is initially 0 and incremented 1 at a time
            if ((bExpr.E1 is UnaryOpExpr uOpExpr4 && uOpExpr4.Op is UnaryOpExpr.Opcode.Cardinality ||
                 bExpr.E1 is ExprDotName exprDName4 && exprDName4.SuffixName == "Length") &&
                ((bExpr.Op is BinaryExpr.Opcode.Lt && replacementOp is BinaryExpr.Opcode.Neq) ||
                 (bExpr.Op is BinaryExpr.Opcode.Neq && replacementOp is BinaryExpr.Opcode.Lt)))
                return true;
        }
        return false;
    }

    private bool IsEquivalentRORLoopGuardTarget(BinaryExpr bExpr) {
        if (_parentLoopGuardBody == null) return false;
        _currentLoopBoundVar = bExpr.E0 is NameSegment nSegExpr0 ? nSegExpr0 :
            bExpr.E1 is NameSegment nSegExpr1 ? nSegExpr1 : null; 
        if (_currentLoopBoundVar == null) return false;
        
        if (!(_assigns.TryGetValue(_currentLoopBoundVar.Name, out var val) &&
              val is LiteralExpr litExpr3 && litExpr3.Value is BigInteger bi3 && bi3 == BigInteger.Zero))
            return false;
        
        _loopBoundVarUpdates = [];
        _visitFurther = false;
        HandleStatement(_parentLoopGuardBody);
        _visitFurther = true;
        if (_loopBoundVarUpdates.Count != 1 || _loopBoundVarUpdates[0] is not BinaryExpr incrementExpr) 
            return false;
        if (incrementExpr.E0 is NameSegment nSegExpr2 && nSegExpr2.Name == _currentLoopBoundVar.Name && 
            incrementExpr.E1 is LiteralExpr litExpr1 && litExpr1.Value is BigInteger bi1 && bi1 == BigInteger.One &&
            incrementExpr.Op == BinaryExpr.Opcode.Add)
            return true;
        if (incrementExpr.E0 is LiteralExpr litExpr2 && litExpr2.Value is BigInteger bi2 && bi2 == BigInteger.One && 
            incrementExpr.E1 is NameSegment nSegExpr3 && nSegExpr3.Name == _currentLoopBoundVar.Name && 
            incrementExpr.Op == BinaryExpr.Opcode.Add)
            return true;
        return false;
    }

    private bool ExcludeSWSTarget(Statement stmt) {
        return stmt is PrintStmt || stmt is OpaqueBlock || stmt is PredicateStmt || stmt is CalcStmt ||
               (stmt is VarDeclStmt && stmt.CoveredTokens.Select((e) => e.val).Contains("ghost")) ||
               (stmt is AssignStatement aStmt && ContainsLemmaChild(aStmt));
    }

    private bool ExcludeSWSTarget(Statement currStmt, Statement prevStmt) {
        if ((currStmt is ReturnStmt && prevStmt is not VarDeclStmt) || 
            currStmt is BreakOrContinueStmt || prevStmt is ReturnStmt || prevStmt is BreakOrContinueStmt)
            return false;
        
        // prevStmt initializes variables used in currStmt (SWS will be invalid)
        if (prevStmt is VarDeclStmt vDeclStmt1 && 
            vDeclStmt1.Locals.Select(e => e.Name)
                .Intersect(_coveredVariableNames).Any())
            return true;
        // prevStmt does not update variables used in currStmt (SWS will have no effect)
        if (prevStmt is AssignStatement aStmt1 && 
            aStmt1.Lhss.Select(lhs => (lhs as NameSegment)?.Name)
                .Intersect(_coveredVariableNames).Any())
            return true;
        
        // currStmt does not update variables used in prevStmt (SWS will have no effect)
        if (currStmt is VarDeclStmt vDeclStmt2 && 
            !vDeclStmt2.Locals.Select(e => e.Name).
                Intersect(_prevCoveredVariableNames).Any())
            return true;
        if (currStmt is AssignStatement aStmt2 && 
            !aStmt2.Lhss.Select(lhs => (lhs as NameSegment)?.Name)
                .Intersect(_prevCoveredVariableNames).Any())
            return true;
        
        if (!_coveredVariableNames.Intersect(_prevCoveredVariableNames).Any())
            return true;
        
        return false;
    }
    
    /// -------------------------------------
    /// Group of overriden top level visitors
    /// -------------------------------------
    public override void Find(ModuleDefinition module) {
        if (module.EndToken.pos != 0)
            _currentScope = (module.StartToken, module.EndToken);
        base.Find(module);
    }
    
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        var prevCurrentScope = (CloneToken(_currentScope.Item1), CloneToken(_currentScope.Item2));
        _currentScope = (decl.StartToken, decl.EndToken);
        _currentSourceDeclFields = [];
        
        foreach (var member in decl.Members) {
            if (mutationTargetURI != "" && !member.Origin.Uri.LocalPath.Contains(mutationTargetURI))
                continue;
            
            if (member is Field f)
                _currentSourceDeclFields.Add(f.Name);
            if (member is not ConstantField cf) continue;
            OriginalFields.Add(cf);
            
            if (!ShouldImplement("VDL") || mutationTargetMethod != "" || mutationTargetRange != (-1, -1) || mutationTargetLine != -1)
                break;
            _varsToDelete.Add(cf.Name);
            AddTarget(($"{_currentScope.Item1?.pos}-{_currentScope.Item2?.pos}", "VDL", cf.Name));
        }
        base.HandleMemberDecls(decl);
        IsFirstVisit = false;
        ScanThisKeywordTargets(decl);
        ScanMBRTargets(decl);
        IsFirstVisit = true;
        _currentScope = prevCurrentScope;
        _varsToDelete = [];
    }
    
    protected override void HandleMethod(Method method) {
        if (mutationTargetMethod != "" && method.Name != mutationTargetMethod)
            return;
        
        _isCurrentMethodVoid = method.Outs.Count == 0;
        _currentMethodOuts = method.Outs.Select(o => o.Name).ToList();
        _currentInitMethodOuts = [];
        _parentBlockHasStmt = false;
        
        base.HandleMethod(method);
        if (ShouldImplement("SDL") && IsIncludedInTarget(method) &&
            (mutationTargetMethod == "" || method.Name == mutationTargetMethod) &&
            _isCurrentMethodVoid && _parentBlockHasStmt)
            AddTarget(($"{method.StartToken.pos}-{method.EndToken.pos}", "SDL", ""));
    }

    /// -------------------------------------
    /// Group of overriden statement visitors
    /// -------------------------------------
    protected override void HandleStatement(Statement stmt) {
        OriginalStmts.Add(stmt);
        base.HandleStatement(stmt);
    }

    protected override void HandleBlock(BlockStmt blockStmt) {
        var prevCurrentScope = (CloneToken(_currentScope.Item1), CloneToken(_currentScope.Item2));
        _currentScope = (blockStmt.StartToken, blockStmt.EndToken);
        var prevAssigns = new Dictionary<string, Expression>(_assigns);
        var prevVarsToDelete = _varsToDelete.Select(item => (string)item.Clone()).ToList();
        
        base.HandleBlock(blockStmt);
        
        _currentScope = prevCurrentScope;
        _assigns = prevAssigns;
        _varsToDelete = prevVarsToDelete;
    }
    
    protected override void HandleBlock(List<Statement> statements) {
        Statement? prevStmt = null;
        var allCoveredVariableNames = _coveredVariableNames;
        var allPrevCoveredVariableNames = _prevCoveredVariableNames;
            
        foreach (var stmt in statements) {
            _prevCoveredVariableNames = _coveredVariableNames;
            _coveredVariableNames = []; // collect vars used in next statement
            if (stmt is not PredicateStmt && stmt is not CalcStmt)
                _parentBlockHasStmt = true;
            
            HandleStatement(stmt);
            if (!ShouldImplement("SWS")) continue;
            if (prevStmt == null || ExcludeSWSTarget(prevStmt) || ExcludeSWSTarget(stmt)) {
                prevStmt = stmt;
                continue;
            }
            
            if (IsIncludedInTarget(prevStmt) && IsIncludedInTarget(prevStmt) && 
                !ExcludeSWSTarget(stmt, prevStmt))
                AddTarget(($"{stmt.StartToken.pos}-{stmt.EndToken.pos}", "SWS", ""));
            prevStmt = stmt;
            allCoveredVariableNames = allCoveredVariableNames.Concat(_coveredVariableNames).ToList();
        }
        _coveredVariableNames = allCoveredVariableNames;
        _prevCoveredVariableNames = allPrevCoveredVariableNames;
    }
    
    protected override void VisitStatement(ConcreteAssignStatement cAStmt) {
        var canMutate = true;
        foreach (var lhs in cAStmt.Lhss) {
            string name;
            if (lhs is NameSegment nSegExpr) {
                name = nSegExpr.Name;
            } else if (lhs is IdentifierExpr idExpr) {
                name = idExpr.Name;
            } else continue;
            if (_currentMethodOuts.Contains(name) && !_currentInitMethodOuts.Contains(name)) {
                // output variable is initialized in this statement
                canMutate = false;
                _currentInitMethodOuts.Add(name);
            }
        }
        
        if (!_isParentVarDeclStmt && ShouldImplement("SDL") && 
            IsIncludedInTarget(cAStmt) && canMutate)
            AddTarget(($"{cAStmt.StartToken.pos}-{cAStmt.EndToken.pos}", "SDL", ""));

        if (_scanThisKeywordTargets) return;
        base.VisitStatement(cAStmt);
    }
    
    protected override void VisitStatement(AssignStatement aStmt) {
        if (ContainsLemmaChild(aStmt)) return;
        foreach (var (lhs, i) in aStmt.Lhss.Select((lhs, i) => (lhs, i))) {
            if (lhs != null && aStmt.Rhss[i] is ExprRhs rhs) {
                _assigns.AddOrUpdate(lhs.ToString(), rhs.Expr, (_, _) => rhs.Expr);
                if (_currentLoopBoundVar != null && lhs.ToString() == _currentLoopBoundVar.Name)
                    _loopBoundVarUpdates.Add(rhs.Expr);
            }
        }
        base.VisitStatement(aStmt);
    }
    
    protected override void VisitStatement(VarDeclStmt vDeclStmt) {        
        _isParentVarDeclStmt = true;
        foreach (var var in vDeclStmt.Locals) {
            if (!ShouldImplement("VDL") || 
                !IsIncludedInTarget(_currentScope.Item1, _currentScope.Item2))
                break;
            if (_varsToDelete.Contains(var.Name)) continue;
            _varsToDelete.Add(var.Name);
            AddTarget(($"{_currentScope.Item1?.pos}-{_currentScope.Item2?.pos}", "VDL", var.Name));
        }

        base.VisitStatement(vDeclStmt);
        _isParentVarDeclStmt = false;
    }
    
    protected override void VisitStatement(ProduceStmt pStmt) {
        if (ShouldImplement("SDL") && IsIncludedInTarget(pStmt) && pStmt is ReturnStmt && 
            (_currentInitMethodOuts.SequenceEqual(_currentMethodOuts) || _isCurrentMethodVoid))
            // only mutate if method is void or if outputs have been initialized
            AddTarget(($"{pStmt.StartToken.pos}-{pStmt.EndToken.pos}", "SDL", ""));
        base.VisitStatement(pStmt);
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        var prevParentBlockHasStmt = _parentBlockHasStmt;
        
        if (ifStmt.Guard != null) {
            HandleExpression(ifStmt.Guard);
        }

        _parentBlockHasStmt = false;
        HandleBlock(ifStmt.Thn);
        if (ifStmt.Els != null && _isChildIfBlock && ShouldImplement("SDL") &&
            IsIncludedInTarget(ifStmt.Thn) && _parentBlockHasStmt) 
        {
            AddTarget(($"{ifStmt.Thn.StartToken.pos}-{ifStmt.Thn.EndToken.pos}", "SDL", ""));
        } else if (ifStmt.Els == null && ShouldImplement("SDL") && 
            IsIncludedInTarget(ifStmt) && _parentBlockHasStmt) 
        {
            AddTarget(($"{ifStmt.StartToken.pos}-{ifStmt.EndToken.pos}", "SDL", ""));
            _isChildIfBlock = false;
        } else if (ifStmt.Els != null && !_isChildIfBlock) {
            _isChildIfBlock = true;
        }
        
        _parentBlockHasStmt = false;
        if (ifStmt.Els != null) {
            if (ifStmt.Els is BlockStmt bEls) {
                HandleBlock(bEls);
                if (ShouldImplement("SDL") && _parentBlockHasStmt && IsIncludedInTarget(ifStmt.Els)) {
                    AddTarget(($"{ifStmt.Els.StartToken.pos}-{ifStmt.Els.EndToken.pos}", "SDL", ""));
                    _isChildIfBlock = false;
                }
            } else {
                HandleStatement(ifStmt.Els);
            }
        }
        _parentBlockHasStmt = prevParentBlockHasStmt;
        
        if (ShouldImplement("CBE") && IsIncludedInTarget(ifStmt)) {
            AddTarget(($"{ifStmt.Thn.StartToken.pos}-{ifStmt.Thn.EndToken.pos}", "CBE", ""));
            if (ifStmt.Els != null && ifStmt.Els is BlockStmt)
                AddTarget(($"{ifStmt.Els.StartToken.pos}-{ifStmt.Els.EndToken.pos}", "CBE", ""));
        }
    }
    
    private void VisitStatement(LoopStmt loopStmt) {
        if (loopStmt.Decreases.Expressions == null) return;
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        foreach (var invariant in loopStmt.Invariants)
            HandleExpression(invariant.E);
        foreach (var decreases in loopStmt.Decreases.Expressions)
            HandleExpression(decreases);
        if (loopStmt.Mod.Expressions != null) {
            foreach (var modifies in loopStmt.Mod.Expressions)
                HandleExpression(modifies.E);
        }
        IsParentSpec = previouslyInsideSpec;
    }
    
    protected override void VisitStatement(WhileStmt whileStmt) {
        if (ShouldImplement("LBI") && IsIncludedInTarget(whileStmt))
            AddTarget(($"{whileStmt.StartToken.pos}-{whileStmt.EndToken.pos}", "LBI", ""));
        VisitStatement(whileStmt as LoopStmt);
        
        if (IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos)) {
            _parentLoopGuardBody = whileStmt.Body;
            HandleExpression(whileStmt.Guard);
            _parentLoopGuardBody = null;
        }
        if (whileStmt.Body != null) HandleBlock(whileStmt.Body);  
    }
    
    protected override void VisitStatement(ForLoopStmt forStmt) {
        if (ShouldImplement("LBI") && IsIncludedInTarget(forStmt))
            AddTarget(($"{forStmt.StartToken.pos}-{forStmt.EndToken.pos}", "LBI", ""));
        VisitStatement(forStmt as LoopStmt);
        base.VisitStatement(forStmt);
    }
    
    protected override void VisitStatement(ForallStmt forStmt) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        foreach (var ensures in forStmt.Ens)
            HandleExpression(ensures.E);
        IsParentSpec = previouslyInsideSpec;
        base.VisitStatement(forStmt);
    }
    
    protected override void VisitStatement(BreakOrContinueStmt bcStmt) {
        if (!IsIncludedInTarget(bcStmt)) return;
        
        if (ShouldImplement("LSR")) {
            if (bcStmt.IsContinue) {
                AddTarget(($"{bcStmt.Center.pos}", "LSR", "break"));
            } else {
                AddTarget(($"{bcStmt.Center.pos}", "LSR", "continue"));
                if (!_isCurrentMethodVoid) return;
                AddTarget(($"{bcStmt.Center.pos}", "LSR", "return"));
            }
        }
        if (ShouldImplement("SDL")) {
            AddTarget(($"{bcStmt.StartToken.pos}-{bcStmt.EndToken.pos}", "SDL", ""));
        }
    }
    
    protected override void VisitStatement(AlternativeLoopStmt altLStmt) {
        if (ShouldImplement("SDL") && IsIncludedInTarget(altLStmt) && altLStmt.Alternatives.Count > 1) { // stmt must have at least one alternative
            foreach (var alt in altLStmt.Alternatives) {
                AddTarget(($"{alt.Guard.StartToken.pos}-{alt.Guard.EndToken.pos}", "SDL", ""));
            }
        }
        VisitStatement(altLStmt as LoopStmt);
        base.VisitStatement(altLStmt);
    }
    
    protected override void VisitStatement(NestedMatchStmt nMatchStmt) {
        if (nMatchStmt.Cases.Count <= 1) {
            base.VisitStatement(nMatchStmt);
            return;
        }

        var hasDefaultCase = false;
        foreach (var cs in nMatchStmt.Cases) {
            if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) {
                hasDefaultCase = true;
                continue;
            }
            if (ShouldImplement("SDL") && IsIncludedInTarget(cs))
                AddTarget(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "SDL", ""));
        }
        if (ShouldImplement("CBR") && IsIncludedInTarget(nMatchStmt) && hasDefaultCase) {
            foreach (var cs in nMatchStmt.Cases) {
                if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) continue;
                AddTarget(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "CBR", ""));
            }
        }
        base.VisitStatement(nMatchStmt);
    }
    
    protected override void VisitStatement(ModifyStmt mdStmt) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        if (mdStmt.Mod.Expressions != null) {
            foreach (var modifies in mdStmt.Mod.Expressions)
                HandleExpression(modifies.E);
        }
        IsParentSpec = previouslyInsideSpec;
        base.VisitStatement(mdStmt);
    }
    
    protected override void VisitStatement(BlockByProofStmt bBpStmt) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        HandleStatement(bBpStmt.Proof);
        IsParentSpec = previouslyInsideSpec;
        base.VisitStatement(bBpStmt);
    }

    protected override void VisitStatement(OpaqueBlock opqBlock) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        foreach (var ensures in opqBlock.Ensures)
            HandleExpression(ensures.E);
        if (opqBlock.Modifies.Expressions != null) {
            foreach (var modifies in opqBlock.Modifies.Expressions)
                HandleExpression(modifies.E);
        }
        IsParentSpec = previouslyInsideSpec;
    }
    
    protected override void VisitStatement(PredicateStmt predStmt) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        HandleExpression(predStmt.Expr);
        IsParentSpec = previouslyInsideSpec;
    }
    
    protected override void VisitStatement(CalcStmt calcStmt) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        HandleExprList(calcStmt.Lines);
        foreach (var stmt in calcStmt.Hints) {
            HandleStatement(stmt);
        }
        IsParentSpec = previouslyInsideSpec;
    }

    /// --------------------------------------
    /// Group of overriden expression visitors
    /// --------------------------------------
    protected override void HandleExpression(Expression expr) {
        if (!_visitFurther)
            return;
        base.HandleExpression(expr);
    }
    
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (!_replacementList.TryGetValue(bExpr.Op, out var replacementList)) 
            return;
        if (!_replacementOpType.TryGetValue(bExpr.Op, out var opName))
            return;
        if (ShouldImplement(opName) && IsIncludedInTarget(bExpr)) {
            foreach (var replacement in replacementList) {
                if (!ExcludeBORTarget(bExpr, replacement))
                    AddTarget(($"{bExpr.Center.pos}", opName, replacement.ToString()));
            }
        }
        
        if (ShouldImplement("ODL") && !CoveredOperators.Contains(bExpr.Op) &&
            mutationTargetMethod == "" && mutationTargetRange == (-1, -1) && mutationTargetLine == -1) 
        {
            CoveredOperators.Add(bExpr.Op);
            AddTarget(("-", "ODL", $"{bExpr.Op.ToString()}-left"));
            AddTarget(("-", "ODL", $"{bExpr.Op.ToString()}-right"));
        }
    
        List<BinaryExpr.Opcode> relationalOperators = [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, 
            BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge
        ];
        List<BinaryExpr.Opcode> conditionalOperators = [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Or,
            BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp
        ];
        if (ShouldImplement("BBR") && IsIncludedInTarget(bExpr) && 
            (relationalOperators.Contains(bExpr.Op) || conditionalOperators.Contains(bExpr.Op))) {
            // relational and conditional expressions can be replaced with true/false 
            AddTarget(($"{bExpr.Center.pos}", "BBR", "true"));
            AddTarget(($"{bExpr.Center.pos}", "BBR", "false"));
        }
        
        base.VisitExpression(bExpr);
    }

    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var (e, i) in cExpr.Operands.Select((e, i) => (e, i))) {
            if (ShouldImplement("AOD") && e is NegationExpression nExpr && IsIncludedInTarget(e)) {
                AddTarget(($"{nExpr.Center.pos}", "AOD", ""));
            }
            
            if (i == cExpr.Operators.Count) return;
            // if the lhs operand is at position i of the operands list
            // then the operator is at position i of the operators list
            var op = cExpr.Operators[i];
            
            if (!_replacementList.TryGetValue(op, out var replacementList)) 
                return;
            if (!_replacementOpType.TryGetValue(op, out var opName))
                return;
            if (ShouldImplement(opName) && IsIncludedInTarget(e)) {
                foreach (var replacement in replacementList) {
                    if (!ExcludeBORTarget(new BinaryExpr(null, op, e, cExpr.Operands[i + 1]), replacement))
                        AddTarget(($"{e.Center.pos}", opName, replacement.ToString()));
                }
            }

            if (ShouldImplement("THI") && e is NameSegment nSegExpr && IsIncludedInTarget(nSegExpr) 
                && _scanThisKeywordTargets && _currentMethodIns.Contains(nSegExpr.Name) && 
                _currentSourceDeclFields.Contains(nSegExpr.Name)) {
                AddTarget(($"{nSegExpr.Center.pos}", "THI", ""));
            }
            if (ShouldImplement("THD") && e is ExprDotName exprDName && IsIncludedInTarget(exprDName) && 
                _scanThisKeywordTargets && exprDName.Lhs is ThisExpr && _currentMethodIns.Contains(exprDName.SuffixName)) {
                AddTarget(($"{exprDName.Center.pos}", "THD", ""));
            }
        }
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        if (ShouldImplement("AOD") && IsIncludedInTarget(nExpr)) {
            // arithmetic operator deletion
            AddTarget(($"{nExpr.Center.pos}", "AOD", ""));
            base.VisitExpression(nExpr);  
        }
    }

    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsParentSpec)
            Targets.RemoveAll(t => t.Item3 == nSegExpr.Name);
        if (!_coveredVariableNames.Contains(nSegExpr.Name) && 
            !_currentMethodIns.Contains(nSegExpr.Name)) {
            _coveredVariableNames.Add(nSegExpr.Name);
        } else if (_scanThisKeywordTargets && 
                  _currentMethodIns.Contains(nSegExpr.Name) && 
                  _currentSourceDeclFields.Contains(nSegExpr.Name)) {
            if (ShouldImplement("THI") && IsIncludedInTarget(nSegExpr))
                AddTarget(($"{nSegExpr.Center.pos}", "THI", ""));
        }
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (suffixExpr is not ExprDotName exprDName) {
            base.VisitExpression(suffixExpr);
            return;
        }

        if (_scanThisKeywordTargets && exprDName.Lhs is ThisExpr) {
            var fieldName = exprDName.SuffixName;
            if (!_currentMethodIns.Contains(fieldName)) return;
            if (ShouldImplement("THD") && IsIncludedInTarget(exprDName))
                AddTarget(($"{exprDName.Center.pos}", "THD", ""));
        }      

        var prevScanThisKeywordTargets = _scanThisKeywordTargets;
        _scanThisKeywordTargets = false;
        base.VisitExpression(suffixExpr);
        _scanThisKeywordTargets = prevScanThisKeywordTargets;
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        if (ShouldImplement("CBE")) {
            if (IsIncludedInTarget(iteExpr.Thn))
                AddTarget(($"{iteExpr.Thn.StartToken.pos}-{iteExpr.Thn.EndToken.pos}", "CBE", ""));
            if (IsIncludedInTarget(iteExpr.Els))
                AddTarget(($"{iteExpr.Els.StartToken.pos}-{iteExpr.Els.EndToken.pos}", "CBE", ""));
        }
        base.VisitExpression(iteExpr);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        if (nMExpr.Cases.Count <= 1) {
            base.VisitExpression(nMExpr);
            return;
        }

        var hasDefaultCase = false;
        foreach (var cs in nMExpr.Cases) {
            if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) {
                hasDefaultCase = true;
                continue;
            }
            if (ShouldImplement("SDL") && IsIncludedInTarget(cs))
                AddTarget(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "SDL", ""));
        }
        if (ShouldImplement("CBR") && hasDefaultCase && IsIncludedInTarget(nMExpr)) {
            foreach (var cs in nMExpr.Cases) {
                if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) continue;
                AddTarget(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "CBR", ""));
            }
        }
        base.VisitExpression(nMExpr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        if (!seqSExpr.SelectOne && seqSExpr.E0 != null && 
            ShouldImplement("SLD") && IsIncludedInTarget(seqSExpr.E0))
            AddTarget(($"{seqSExpr.E0.Center.pos}", "SLD", ""));
        if (!seqSExpr.SelectOne && seqSExpr.E1 != null && 
            ShouldImplement("SLD") && IsIncludedInTarget(seqSExpr.E1))
            AddTarget(($"{seqSExpr.E1.Center.pos}", "SLD", ""));
        base.VisitExpression(seqSExpr);
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        if (compExpr is LambdaExpr lExpr && lExpr.Reads.Expressions != null) {
            foreach (var reads in lExpr.Reads.Expressions)
                HandleExpression(reads.E);                
        }
        IsParentSpec = previouslyInsideSpec;
        base.VisitExpression(compExpr);
    }

    protected override void VisitExpression(OldExpr oldExpr) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        HandleExpression(oldExpr.E);
        IsParentSpec = previouslyInsideSpec;
    }
    
    protected override void VisitExpression(UnchangedExpr unchExpr) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        foreach (var unchanged in unchExpr.Frame)
            HandleExpression(unchanged.E);            
        IsParentSpec = previouslyInsideSpec;
    }
    
    protected override void VisitExpression(DecreasesToExpr dToExpr) {
        var previouslyInsideSpec = IsParentSpec;
        IsParentSpec = true;
        foreach (var oldExpr in dToExpr.OldExpressions)
            HandleExpression(oldExpr); 
        foreach (var newExpr in dToExpr.NewExpressions)
            HandleExpression(newExpr); 
        IsParentSpec = previouslyInsideSpec;
    }
}