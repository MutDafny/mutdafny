using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class PreResolveTargetScanner(List<string> operatorsInUse, ErrorReporter reporter)
    : TargetScanner(operatorsInUse, reporter)
{
    private List<string> _coveredVariableNames = [];
    private List<BinaryExpr.Opcode> _coveredOperators = [];
    private string _currentMethodScope = "";
    private List<string> _currentMethodOuts = [];
    private List<string> _currentInitMethodOuts = [];
    private bool _isCurrentMethodVoid;
    private bool _isChildIfBlock;
    private bool _isParentVarDeclStmt;
    
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
    
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member is not ConstantField cf) continue;
            if (!ShouldImplement("VDL")) break;
            _coveredVariableNames.Add(cf.Name);
            Targets.Add(("-", "VDL", cf.Name));
        }
        base.HandleMemberDecls(decl);
    }

    protected override void HandleMethod(Method method) {
        var methodIndependentVars = _coveredVariableNames.Select(item => (string)item.Clone()).ToList();
        _isCurrentMethodVoid = method.Outs.Count == 0;
        _currentMethodScope = $"{method.StartToken.pos}-{method.EndToken.pos}";
        _currentMethodOuts = method.Outs.Select(o => o.Name).ToList();
        _currentInitMethodOuts = [];
        if (ShouldImplement("SDL") && _isCurrentMethodVoid)
            Targets.Add(($"{method.StartToken.pos}-{method.EndToken.pos}", "SDL", ""));
        
        base.HandleMethod(method);
        _currentMethodScope = "-";
        _coveredVariableNames = methodIndependentVars;
    }

    /// -------------------------------------
    /// Group of overriden statement visitors
    /// -------------------------------------
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
        if (IsParentSpec && _coveredVariableNames.Contains(nSegExpr.Name)) {
            _coveredVariableNames.Remove(nSegExpr.Name);
            Targets.RemoveAll(t => t.Item3 == nSegExpr.Name);
        }
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (suffixExpr is not ExprDotName exprDName) return;
        if (IsParentSpec && _coveredVariableNames.Contains(exprDName.SuffixName)) {
            _coveredVariableNames.Remove(exprDName.SuffixName);
            Targets.RemoveAll(t => t.Item3 == exprDName.SuffixName);
        }
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
        if (!seqSExpr.SelectOne && seqSExpr.E0 != null && ShouldImplement("SDL"))
            Targets.Add(($"{seqSExpr.E0.Center.pos}", "SDL", ""));
        if (!seqSExpr.SelectOne && seqSExpr.E1 != null && ShouldImplement("SDL"))
            Targets.Add(($"{seqSExpr.E1.Center.pos}", "SDL", ""));
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