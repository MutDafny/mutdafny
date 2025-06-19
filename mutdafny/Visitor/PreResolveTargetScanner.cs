using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class PreResolveTargetScanner(List<string> operatorsInUse, ErrorReporter reporter)
    : TargetScanner(operatorsInUse, reporter)
{
    private List<string> _coveredVariableNames = [];
    private List<string> _specCoveredVariableNames = [];
    private readonly List<BinaryExpr.Opcode> _coveredOperators = [];
    private string _currentMethodScope = "";
    private List<string> _currentMethodIns = [];
    private List<string> _currentMethodOuts = [];
    private List<string> _currentInitMethodOuts = [];
    private List<string> _currentSourceDeclFields = [];
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
        // set inclusion operators
        { BinaryExpr.Opcode.In, [BinaryExpr.Opcode.NotIn] },
        { BinaryExpr.Opcode.NotIn, [BinaryExpr.Opcode.In] },
    };

    private void ScanThisKeywordTargets(TopLevelDeclWithMembers decl) {
        if (!ShouldImplement("THI") && !ShouldImplement("THD")) return;
        foreach (var member in decl.Members) {
            if (member.IsGhost) continue; // only searches for mutation targets in non-ghost constructs

            if (member is MethodOrFunction mf) {
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
                        Targets.Add(($"{m1.StartToken.pos}-{m1.EndToken.pos}", "AMR", $"{m2.StartToken.pos}-{m2.EndToken.pos}"));
                } else {
                    var m1InTypes = m1.Ins.Select(i => i.Type.ToString()).ToList();
                    var m2InTypes = m2.Ins.Select(i => i.Type.ToString()).ToList();
                    if (m1InTypes.SequenceEqual(m2InTypes))
                        Targets.Add(($"{m1.StartToken.pos}-{m1.EndToken.pos}", "MMR", $"{m2.StartToken.pos}-{m2.EndToken.pos}"));
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

    private bool ExcludeSWSTarget(Statement stmt) {
        return stmt is PrintStmt || stmt is OpaqueBlock || stmt is PredicateStmt || stmt is CalcStmt;
    }
    
    /// -------------------------------------
    /// Group of overriden top level visitors
    /// -------------------------------------
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        _currentSourceDeclFields = [];
        foreach (var member in decl.Members) {
            if (member is Field f)
                _currentSourceDeclFields.Add(f.Name);
            
            if (member is not ConstantField cf) continue;
            if (!ShouldImplement("VDL")) break;
            _coveredVariableNames.Add(cf.Name);
            Targets.Add(("-", "VDL", cf.Name));
        }
        base.HandleMemberDecls(decl);
        IsFirstVisit = false;
        ScanThisKeywordTargets(decl);
        ScanMBRTargets(decl);
        IsFirstVisit = true;
    }
    
    protected override void HandleMethod(Method method) {
        var methodIndependentVars = _coveredVariableNames.Select(item => (string)item.Clone()).ToList();
        _isCurrentMethodVoid = method.Outs.Count == 0;
        _currentMethodScope = $"{method.StartToken.pos}-{method.EndToken.pos}";
        _currentMethodOuts = method.Outs.Select(o => o.Name).ToList();
        _currentInitMethodOuts = [];
        _parentBlockHasStmt = false;
        
        base.HandleMethod(method);
        if (ShouldImplement("SDL") && _isCurrentMethodVoid && _parentBlockHasStmt)
            Targets.Add(($"{method.StartToken.pos}-{method.EndToken.pos}", "SDL", ""));

        _currentMethodScope = "-";
        _coveredVariableNames = methodIndependentVars;
    }

    /// -------------------------------------
    /// Group of overriden statement visitors
    /// -------------------------------------
    protected override void HandleBlock(List<Statement> statements) {
        Statement? prevStmt = null;
        foreach (var stmt in statements) {
            var prevCoveredVariableNames = _coveredVariableNames;
            _coveredVariableNames = []; // collect vars used in next statement
            _specCoveredVariableNames = [];

            if (stmt is not PredicateStmt && stmt is not CalcStmt)
                _parentBlockHasStmt = true;
            
            HandleStatement(stmt);
            _coveredVariableNames = _coveredVariableNames.Concat(_specCoveredVariableNames).ToList();
            if (!ShouldImplement("SWS")) continue;
            if (prevStmt == null || ExcludeSWSTarget(prevStmt) || ExcludeSWSTarget(stmt)) {
                prevStmt = stmt;
                continue;
            }
            
            if (prevStmt is not VarDeclStmt vDeclStmt) {
                Targets.Add(($"{stmt.StartToken.pos}-{stmt.EndToken.pos}", "SWS", ""));
            } else if (!vDeclStmt.Locals.Select(e => e.Name).Intersect(_coveredVariableNames).Any()) {
                Targets.Add(($"{stmt.StartToken.pos}-{stmt.EndToken.pos}", "SWS", ""));
            }
            prevStmt = stmt;
            _coveredVariableNames = prevCoveredVariableNames.Concat(_coveredVariableNames).Distinct().ToList(); 
        }
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
        
        if (!_isParentVarDeclStmt && ShouldImplement("SDL") && canMutate) {
            Targets.Add(($"{cAStmt.StartToken.pos}-{cAStmt.EndToken.pos}", "SDL", ""));
        }

        if (_scanThisKeywordTargets) return;
        base.VisitStatement(cAStmt);
    }
    
    protected override void VisitStatement(VarDeclStmt vDeclStmt) {
        _isParentVarDeclStmt = true;
        foreach (var var in vDeclStmt.Locals) {
            if (!ShouldImplement("VDL")) break;
            if (_coveredVariableNames.Contains(var.Name)) continue;
            _coveredVariableNames.Add(var.Name);
            Targets.Add((_currentMethodScope, "VDL", var.Name));
        }

        base.VisitStatement(vDeclStmt);
        _isParentVarDeclStmt = false;
    }
    
    protected override void VisitStatement(ProduceStmt pStmt) {
        if (ShouldImplement("SDL") && pStmt is ReturnStmt && 
            (_currentInitMethodOuts.SequenceEqual(_currentMethodOuts) || _isCurrentMethodVoid)) {
            // only mutate if method is void or if outputs have been initialized
            Targets.Add(($"{pStmt.StartToken.pos}-{pStmt.EndToken.pos}", "SDL", ""));
        }
        base.VisitStatement(pStmt);
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (ShouldImplement("SDL")) {
            if (ifStmt.Els == null) {
                Targets.Add(($"{ifStmt.StartToken.pos}-{ifStmt.EndToken.pos}", "SDL", ""));
                _isChildIfBlock = false;
            } else if (_isChildIfBlock) {
                Targets.Add(($"{ifStmt.Thn.StartToken.pos}-{ifStmt.Thn.EndToken.pos}", "SDL", ""));
            } else { // first block
                _isChildIfBlock = true;
            }

            if (ifStmt.Els is BlockStmt) { // last block
                Targets.Add(($"{ifStmt.Els.StartToken.pos}-{ifStmt.Els.EndToken.pos}", "SDL", ""));
                _isChildIfBlock = false;
            }
        }
        
        if (ShouldImplement("CBE")) {
            Targets.Add(($"{ifStmt.Thn.StartToken.pos}-{ifStmt.Thn.EndToken.pos}", "CBE", ""));
            if (ifStmt.Els != null && ifStmt.Els is BlockStmt)
                Targets.Add(($"{ifStmt.Els.StartToken.pos}-{ifStmt.Els.EndToken.pos}", "CBE", ""));
        }

        base.VisitStatement(ifStmt);
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
        if (ShouldImplement("LBI"))
            Targets.Add(($"{whileStmt.StartToken.pos}-{whileStmt.EndToken.pos}", "LBI", ""));
        VisitStatement(whileStmt as LoopStmt);
        base.VisitStatement(whileStmt);
    }
    
    protected override void VisitStatement(ForLoopStmt forStmt) {
        if (ShouldImplement("LBI"))
            Targets.Add(($"{forStmt.StartToken.pos}-{forStmt.EndToken.pos}", "LBI", ""));
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
        if (ShouldImplement("LSR")) {
            if (bcStmt.IsContinue) {
                Targets.Add(($"{bcStmt.Center.pos}", "LSR", "break"));
            } else {
                Targets.Add(($"{bcStmt.Center.pos}", "LSR", "continue"));
                if (!_isCurrentMethodVoid) return;
                Targets.Add(($"{bcStmt.Center.pos}", "LSR", "return"));
            }
        }
        if (ShouldImplement("SDL")) {
            Targets.Add(($"{bcStmt.StartToken.pos}-{bcStmt.EndToken.pos}", "SDL", ""));
        }
    }
    
    protected override void VisitStatement(AlternativeLoopStmt altLStmt) {
        if (ShouldImplement("SDL") && altLStmt.Alternatives.Count > 1) { // stmt must have at least one alternative
            foreach (var alt in altLStmt.Alternatives) {
                Targets.Add(($"{alt.Guard.StartToken.pos}-{alt.Guard.EndToken.pos}", "SDL", ""));
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
            if (ShouldImplement("SDL"))
                Targets.Add(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "SDL", ""));
        }
        if (ShouldImplement("CBR") && hasDefaultCase) {
            foreach (var cs in nMatchStmt.Cases) {
                if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) continue;
                Targets.Add(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "CBR", ""));
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
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (!_replacementList.TryGetValue(bExpr.Op, out var replacementList)) 
            return;
        if (ShouldImplement("BOR")) {
            foreach (var replacement in replacementList)
                Targets.Add(($"{bExpr.Center.pos}", "BOR", replacement.ToString()));
        }
        if (ShouldImplement("ODL") && !_coveredOperators.Contains(bExpr.Op)) {
            _coveredOperators.Add(bExpr.Op);
            Targets.Add(("-", "ODL", $"{bExpr.Op.ToString()}-left"));
            Targets.Add(("-", "ODL", $"{bExpr.Op.ToString()}-right"));
        }
    
        List<BinaryExpr.Opcode> relationalOperators = [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, 
            BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge
        ];
        List<BinaryExpr.Opcode> conditionalOperators = [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Or,
            BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp
        ];
        if (ShouldImplement("BBR") && 
            (relationalOperators.Contains(bExpr.Op) || conditionalOperators.Contains(bExpr.Op))) {
            // relational and conditional expressions can be replaced with true/false 
            Targets.Add(($"{bExpr.Center.pos}", "BBR", "true"));
            Targets.Add(($"{bExpr.Center.pos}", "BBR", "false"));
        }
        
        base.VisitExpression(bExpr);
    }

    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var (e, i) in cExpr.Operands.Select((e, i) => (e, i))) {
            if (ShouldImplement("UOD") && e is NegationExpression nExpr) {
                Targets.Add(($"{nExpr.Center.pos}", "UOD", ""));
            }
            
            if (i == cExpr.Operators.Count) return;
            // if the lhs operand is at position i of the operands list
            // then the operator is at position i of the operators list
            var op = cExpr.Operators[i];
            
            if (!_replacementList.TryGetValue(op, out var replacementList)) 
                return;
            if (ShouldImplement("BOR")) {
                foreach (var replacement in replacementList) {
                    Targets.Add(($"{e.Center.pos}", "BOR", replacement.ToString()));
                }
            }

            if (ShouldImplement("THI") && e is NameSegment nSegExpr && _scanThisKeywordTargets &&
                _currentMethodIns.Contains(nSegExpr.Name) && _currentSourceDeclFields.Contains(nSegExpr.Name)) {
                Targets.Add(($"{nSegExpr.Center.pos}", "THI", ""));
            }
            if (ShouldImplement("THD") && e is ExprDotName exprDName && _scanThisKeywordTargets &&
                exprDName.Lhs is ThisExpr && _currentMethodIns.Contains(exprDName.SuffixName)) {
                Targets.Add(($"{exprDName.Center.pos}", "THD", ""));
            }
        }
    }
    
    protected override void VisitExpression(UnaryExpr uExpr) {
        if (ShouldImplement("UOD") && uExpr is UnaryOpExpr uOpExpr && uOpExpr.Op == UnaryOpExpr.Opcode.Not) {
            // conditional/logical operator deletion
            Targets.Add(($"{uOpExpr.Center.pos}", "UOD", ""));
        }
        
        base.VisitExpression(uExpr);
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        if (ShouldImplement("UOD")) {
            // arithmetic operator deletion
            Targets.Add(($"{nExpr.Center.pos}", "UOD", ""));
            base.VisitExpression(nExpr);  
        }
    }

    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsParentSpec) {
            if (_coveredVariableNames.Contains(nSegExpr.Name))
                _coveredVariableNames.Remove(nSegExpr.Name);
            _specCoveredVariableNames.Add(nSegExpr.Name);
            Targets.RemoveAll(t => t.Item3 == nSegExpr.Name);
        } else if (!_coveredVariableNames.Contains(nSegExpr.Name) && 
                   !_currentMethodIns.Contains(nSegExpr.Name)) {
            _coveredVariableNames.Add(nSegExpr.Name);
        } else if (_scanThisKeywordTargets && 
                  _currentMethodIns.Contains(nSegExpr.Name) && 
                  _currentSourceDeclFields.Contains(nSegExpr.Name)) {
            if (ShouldImplement("THI"))
                Targets.Add(($"{nSegExpr.Center.pos}", "THI", ""));
        }
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (suffixExpr is not ExprDotName exprDName) {
            base.VisitExpression(suffixExpr);
            return;
        }

        if (IsParentSpec) {
            if (_coveredVariableNames.Contains(exprDName.SuffixName))
                _coveredVariableNames.Remove(exprDName.SuffixName);
            _specCoveredVariableNames.Add(exprDName.SuffixName);
            Targets.RemoveAll(t => t.Item3 == exprDName.SuffixName);
        } else if (_scanThisKeywordTargets && exprDName.Lhs is ThisExpr) {
            var fieldName = exprDName.SuffixName;

            if (!_currentMethodIns.Contains(fieldName)) return;
            if (ShouldImplement("THD"))
                Targets.Add(($"{exprDName.Center.pos}", "THD", ""));
        }      

        var prevScanThisKeywordTargets = _scanThisKeywordTargets;
        _scanThisKeywordTargets = false;
        base.VisitExpression(suffixExpr);
        _scanThisKeywordTargets = prevScanThisKeywordTargets;
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        if (ShouldImplement("CBE")) {
            Targets.Add(($"{iteExpr.Thn.StartToken.pos}-{iteExpr.Thn.EndToken.pos}", "CBE", ""));
            Targets.Add(($"{iteExpr.Els.StartToken.pos}-{iteExpr.Els.EndToken.pos}", "CBE", ""));
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
            if (ShouldImplement("SDL"))
                Targets.Add(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "SDL", ""));
        }
        if (ShouldImplement("CBR") && hasDefaultCase) {
            foreach (var cs in nMExpr.Cases) {
                if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) continue;
                Targets.Add(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "CBR", ""));
            }
        }
        base.VisitExpression(nMExpr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        if (!seqSExpr.SelectOne && seqSExpr.E0 != null && ShouldImplement("SLD"))
            Targets.Add(($"{seqSExpr.E0.Center.pos}", "SLD", ""));
        if (!seqSExpr.SelectOne && seqSExpr.E1 != null && ShouldImplement("SLD"))
            Targets.Add(($"{seqSExpr.E1.Center.pos}", "SLD", ""));
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