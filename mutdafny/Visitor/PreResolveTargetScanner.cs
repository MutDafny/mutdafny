using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class PreResolveTargetScanner : TargetScanner
{
    private readonly Dictionary<BinaryExpr.Opcode, List<BinaryExpr.Opcode>> _replacementList;
    private List<string> _currentMethodOuts;
    private List<string> _currentInitMethodOuts;
    private bool _isCurrentMethodVoid;
    private bool _isChildIfBlock;
    private bool _isParentVarDeclStmt;

    public PreResolveTargetScanner(List<string> operatorsInUse, ErrorReporter reporter): base(operatorsInUse, reporter)
    {
       _replacementList = new Dictionary<BinaryExpr.Opcode, List<BinaryExpr.Opcode>> {
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
    }
    
    protected override void HandleMethod(Method method) {
        _isCurrentMethodVoid = method.Outs.Count == 0;
        _currentMethodOuts = method.Outs.Select(o => o.Name).ToList();
        _currentInitMethodOuts = [];
        if (ShouldImplement("SDL") && _isCurrentMethodVoid)
            Targets.Add(($"{method.StartToken.pos}-{method.EndToken.pos}", "SDL", ""));
        
        base.HandleMethod(method);
    }

    /// -------------------------------------
    /// Group of overriden statement visitors
    /// -------------------------------------
    protected override void VisitStatement(ConcreteAssignStatement cAStmt) {
        var canMutate = true;
        foreach (var lhs in cAStmt.Lhss) {
            if (lhs is not NameSegment nSegExpr) continue;
            var name = nSegExpr.Name;
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
    
    protected override void VisitStatement(WhileStmt whileStmt) {
        if (ShouldImplement("LBI"))
            Targets.Add(($"{whileStmt.StartToken.pos}-{whileStmt.EndToken.pos}", "LBI", ""));
        base.VisitStatement(whileStmt);
    }
    
    protected override void VisitStatement(ForLoopStmt forStmt) {
        if (ShouldImplement("LBI"))
            Targets.Add(($"{forStmt.StartToken.pos}-{forStmt.EndToken.pos}", "LBI", ""));
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
        base.VisitStatement(altLStmt);
    }
    
    protected override void VisitStatement(NestedMatchStmt nMatchStmt) {
        if (ShouldImplement("SDL") && nMatchStmt.Cases.Count > 1) { // stmt must have at least one alternative
            foreach (var cs in nMatchStmt.Cases) {
                if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) continue;
                Targets.Add(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "SDL", ""));
            }
        }
        base.VisitStatement(nMatchStmt);
    }

    /// --------------------------------------
    /// Group of overriden expression visitors
    /// --------------------------------------
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (!_replacementList.TryGetValue(bExpr.Op, out var replacementList)) 
            return;
        if (ShouldImplement("BOR")) {
            foreach (var replacement in replacementList) {
                Targets.Add(($"{bExpr.Center.pos}", "BOR", replacement.ToString()));
            }
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
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        if (ShouldImplement("SDL") && nMExpr.Cases.Count > 1) {
            foreach (var cs in nMExpr.Cases) {
                if (cs.Pat is IdPattern idPat && idPat.IsWildcardPattern) continue;
                Targets.Add(($"{cs.StartToken.pos}-{cs.EndToken.pos}", "SDL", ""));
            }
        }
        base.VisitExpression(nMExpr);
    }
}