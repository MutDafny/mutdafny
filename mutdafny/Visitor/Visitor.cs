using Microsoft.Dafny;
using Type = System.Type;

namespace MutDafny.Visitor;

// this is the default implementation of the AST visitor
// other classes that aim to find specific parts of the AST can inherit this class and simply override the necessary statement/expression handlers
// this class can be used to find specific mutation targets, their location given by the MutationTargetPos field
// alternatively it can be used for other purposes, case in which MutationTargetPos should be given as -1
public class Visitor
{
    protected Statement? TargetStatement { get; set; }
    protected Expression? TargetExpression { get; set; }
    protected AssignmentRhs? TargetAssignmentRhs { get; set; }
    
    private readonly Dictionary<Type, Action<Statement>> _statementHandlers;
    private readonly Dictionary<Type, Action<Expression>> _expressionHandlers;
    protected readonly string MutationTargetPos;
    protected readonly ErrorReporter Reporter;
    
    protected Visitor(string mutationTargetPos, ErrorReporter reporter)
    {
        MutationTargetPos = mutationTargetPos;
        Reporter = reporter;
        
        _statementHandlers = new Dictionary<Type, Action<Statement>> {
            {typeof(BlockStmt), stmt => VisitStatement((stmt as BlockStmt)!)},
            {typeof(ConcreteAssignStatement), stmt => VisitStatement((stmt as ConcreteAssignStatement)!)},
            {typeof(AssignStatement), stmt => VisitStatement((stmt as AssignStatement)!)},
            {typeof(AssignSuchThatStmt), stmt => VisitStatement((stmt as AssignSuchThatStmt)!)},
            {typeof(AssignOrReturnStmt), stmt => VisitStatement((stmt as AssignOrReturnStmt)!)},
            {typeof(SingleAssignStmt), stmt => VisitStatement((stmt as SingleAssignStmt)!)},
            {typeof(VarDeclStmt), stmt => VisitStatement((stmt as VarDeclStmt)!)},
            {typeof(VarDeclPattern), stmt => VisitStatement((stmt as VarDeclPattern)!)},
            {typeof(ProduceStmt), stmt => VisitStatement((stmt as ProduceStmt)!)},
            {typeof(IfStmt), stmt => VisitStatement((stmt as IfStmt)!)},
            {typeof(WhileStmt), stmt => VisitStatement((stmt as WhileStmt)!)},
            {typeof(ForLoopStmt), stmt => VisitStatement((stmt as ForLoopStmt)!)},
            {typeof(ForallStmt), stmt => VisitStatement((stmt as ForallStmt)!)},
            {typeof(BreakOrContinueStmt), stmt => VisitStatement((stmt as BreakOrContinueStmt)!)},
            {typeof(AlternativeLoopStmt), stmt => VisitStatement((stmt as AlternativeLoopStmt)!)},
            {typeof(AlternativeStmt), stmt => VisitStatement((stmt as AlternativeStmt)!)},
            {typeof(MatchStmt), stmt => VisitStatement((stmt as MatchStmt)!)},
            {typeof(NestedMatchStmt), stmt => VisitStatement((stmt as NestedMatchStmt)!)},
            {typeof(CallStmt), stmt => VisitStatement((stmt as CallStmt)!)},
            {typeof(ModifyStmt), stmt => VisitStatement((stmt as ModifyStmt)!)},
            {typeof(HideRevealStmt), stmt => VisitStatement((stmt as HideRevealStmt)!)},
            {typeof(BlockByProofStmt), stmt => VisitStatement((stmt as BlockByProofStmt)!)},
            {typeof(SkeletonStatement), stmt => VisitStatement((stmt as SkeletonStatement)!)},
            {typeof(PrintStmt), stmt => VisitStatement((stmt as PrintStmt)!)},
            // spec statements
            {typeof(OpaqueBlock), stmt => VisitStatement((stmt as OpaqueBlock)!)},
            {typeof(PredicateStmt), stmt => VisitStatement((stmt as PredicateStmt)!)},
            {typeof(CalcStmt), stmt => VisitStatement((stmt as CalcStmt)!)},
        };
        _expressionHandlers = new Dictionary<Type, Action<Expression>> {
            {typeof(LiteralExpr), expr => VisitExpression((expr as LiteralExpr)!)},
            {typeof(BinaryExpr), expr => VisitExpression((expr as BinaryExpr)!)},
            {typeof(UnaryExpr), expr => VisitExpression((expr as UnaryExpr)!)},
            {typeof(ParensExpression), expr => VisitExpression((expr as ParensExpression)!)},
            {typeof(NegationExpression), expr => VisitExpression((expr as NegationExpression)!)},
            {typeof(ChainingExpression), expr => VisitExpression((expr as ChainingExpression)!)},
            {typeof(NameSegment), expr => VisitExpression((expr as NameSegment)!)},
            {typeof(LetExpr), expr => VisitExpression((expr as LetExpr)!)},
            {typeof(LetOrFailExpr), expr => VisitExpression((expr as LetOrFailExpr)!)},
            {typeof(ApplyExpr), expr => VisitExpression((expr as ApplyExpr)!)},
            {typeof(SuffixExpr), expr => VisitExpression((expr as SuffixExpr)!)},
            {typeof(FunctionCallExpr), expr => VisitExpression((expr as FunctionCallExpr)!)},
            {typeof(MemberSelectExpr), expr => VisitExpression((expr as MemberSelectExpr)!)},
            {typeof(ITEExpr), expr => VisitExpression((expr as ITEExpr)!)},
            {typeof(MatchExpr), expr => VisitExpression((expr as MatchExpr)!)},
            {typeof(NestedMatchExpr), expr => VisitExpression((expr as NestedMatchExpr)!)},
            {typeof(DisplayExpression), expr => VisitExpression((expr as DisplayExpression)!)},
            {typeof(MapDisplayExpr), expr => VisitExpression((expr as MapDisplayExpr)!)},
            {typeof(SeqConstructionExpr), expr => VisitExpression((expr as SeqConstructionExpr)!)},
            {typeof(MultiSetFormingExpr), expr => VisitExpression((expr as MultiSetFormingExpr)!)},
            {typeof(SeqSelectExpr), expr => VisitExpression((expr as SeqSelectExpr)!)},
            {typeof(MultiSelectExpr), expr => VisitExpression((expr as MultiSelectExpr)!)},
            {typeof(SeqUpdateExpr), expr => VisitExpression((expr as SeqUpdateExpr)!)},    
            {typeof(ComprehensionExpr), expr => VisitExpression((expr as ComprehensionExpr)!)},
            {typeof(DatatypeUpdateExpr), expr => VisitExpression((expr as DatatypeUpdateExpr)!)},
            {typeof(DatatypeValue), expr => VisitExpression((expr as DatatypeValue)!)},
            {typeof(StmtExpr), expr => VisitExpression((expr as StmtExpr)!)},
            // spec expressions
            {typeof(OldExpr), expr => VisitExpression((expr as OldExpr)!)},
            {typeof(UnchangedExpr), expr => VisitExpression((expr as UnchangedExpr)!)},
            {typeof(DecreasesToExpr), expr => VisitExpression((expr as DecreasesToExpr)!)},
        };
    }

    /// ---------------------------
    /// Group of top level visitors
    /// ---------------------------
    public virtual void Find(ModuleDefinition module) {
        // only visit modules that may contain the mutation target
        if (module.EndToken.pos == 0 || // default module
            IsWorthVisiting(module.StartToken.pos, module.EndToken.pos)) 
        {
            HandleDefaultClassDecl(module);
            if (TargetFound()) return;
            HandleSourceDecls(module);

            INode? target;
            if (TargetFound() && (target = GetTarget()) != null) {
                var targetToken = new AutoGeneratedOrigin(target.Origin);
                Reporter.Info(MessageSource.Rewriter, targetToken, 
                    $"This {GetTargetType()} contains the mutation target"
                );
            }
        }
    }

    protected virtual void HandleDefaultClassDecl(ModuleDefinition module) {
        if (module.DefaultClass == null) return;
        HandleMemberDecls(module.DefaultClass);
    }

    protected virtual  void HandleSourceDecls(ModuleDefinition module) {
        foreach (var decl in module.SourceDecls) {
            // only visit declarations that may contain the mutation target
            if (!IsWorthVisiting(decl.StartToken.pos, decl.EndToken.pos)) continue;
            if (decl is TopLevelDeclWithMembers declWithMembers) { // includes class, trait, datatype, etc.
                HandleMemberDecls(declWithMembers);   
            }
            if (decl is IteratorDecl itDecl) {
                HandleBlock(itDecl.Body);
            } else if (decl is NewtypeDecl newTpDecl) {
                HandleExpression(newTpDecl.Constraint);
            } else if (decl is SubsetTypeDecl subTpDecl) {
                HandleExpression(subTpDecl.Constraint);
                if (subTpDecl is NonNullTypeDecl nNullTpDecl) {
                    HandleMemberDecls(nNullTpDecl.Class);
                }
            }
            
            if (TargetFound()) return;
        }
    }

    protected virtual void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member.IsGhost) continue; // only searches for mutation targets in non-ghost constructs
            
            // only visit members that may contain the mutation target
            if (!IsWorthVisiting(member.StartToken.pos, member.EndToken.pos)) continue;
            if (member is Method m) { // includes constructor
                HandleMethod(m);  
            } else if (member is Function func) { // includes predicate
                // only searches for mutation targets in functions/predciates not used in spec
                var specHelpers = MutDafny.SpecHelpers;                
                if (specHelpers.Contains(func.Name)) continue;
                HandleFunction(func);
            } else if (member is ConstantField cf) {
                if (cf.Rhs == null) continue;
                HandleExpression(cf.Rhs);
            }

            if (TargetFound()) return;
        }
    }

    protected virtual void HandleMethod(Method method) {
        var methodToken = new AutoGeneratedOrigin(method.Origin);
        Reporter.Info(MessageSource.Rewriter, methodToken, 
            "This method contains the mutation target"
        );
        
        if (method.Body == null) return;
        HandleBlock(method.Body);
    }

    protected virtual void HandleFunction(Function function) {
        var functionToken = new AutoGeneratedOrigin(function.Origin);
        Reporter.Info(MessageSource.Rewriter, functionToken, 
            $"This {function.WhatKind} contains the mutation target"
        );
        
        if (function.Body == null) return;
        HandleExpression(function.Body);
    }
    
    /// ---------------------------
    /// Group of statement visitors
    /// ---------------------------
    protected virtual void HandleStatement(Statement stmt) {
        var derivedType = stmt.GetType();
        while (derivedType != typeof(object) && derivedType != null) {
            if (_statementHandlers.TryGetValue(derivedType, out var handler)) {
                handler(stmt);
                return;
            }
            derivedType = derivedType.BaseType;
        }
    }
    
    protected virtual void HandleBlock(BlockStmt blockStmt) {
        if (blockStmt is DividedBlockStmt dBlockStmt) {
            HandleBlock(dBlockStmt.BodyInit);
            HandleBlock(dBlockStmt.BodyProper);
        } else {
            HandleBlock(blockStmt.Body);
        }
    }
    
    protected virtual void HandleBlock(List<Statement> statements) {
        foreach (var stmt in statements) {
            if (IsWorthVisiting(stmt.StartToken.pos, stmt.EndToken.pos)) {
                HandleStatement(stmt);
            }
            if (TargetFound()) return;
        }
    }
    
    protected virtual void VisitStatement(BlockStmt blockStmt) {
        HandleBlock(blockStmt);
        if (TargetFound()) return;
        
        if (blockStmt is DividedBlockStmt dBlockStmt) {
            HandleBlock(dBlockStmt.BodyInit);
            if (TargetFound()) return;
            HandleBlock(dBlockStmt.BodyProper);
        }
    }

    protected virtual void VisitStatement(ConcreteAssignStatement cAStmt) {
        HandleExprList(cAStmt.Lhss);
    }

    protected virtual void VisitStatement(AssignStatement aStmt) {
        VisitStatement(aStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleRhsList(aStmt.Rhss); 
        if (TargetFound() || aStmt.OriginalInitialLhs == null) 
            return;
        HandleExpression(aStmt.OriginalInitialLhs);
    }
    
    protected virtual void VisitStatement(AssignSuchThatStmt aStStmt) {
        VisitStatement(aStStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleExpression(aStStmt.Expr);
    }
    
    protected virtual void VisitStatement(AssignOrReturnStmt aOrRStmt) {
        VisitStatement(aOrRStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleExpression(aOrRStmt.Rhs.Expr);
        if (TargetFound()) return;
        HandleRhsList(aOrRStmt.Rhss);
    }

    protected virtual void VisitStatement(SingleAssignStmt sAStmt) {
        if (IsWorthVisiting(sAStmt.Lhs.StartToken.pos, sAStmt.Lhs.EndToken.pos)) {
            HandleExpression(sAStmt.Lhs);
        } 
        HandleRhsList([sAStmt.Rhs]);
    }

    protected virtual void VisitStatement(VarDeclStmt vDeclStmt) {
        if (vDeclStmt.Assign == null) return;
        HandleStatement(vDeclStmt.Assign);
    }

    protected virtual void VisitStatement(VarDeclPattern vDeclPStmt) {
        HandleExpression(vDeclPStmt.RHS);
    }

    // includes ReturnStmt and YieldStmt
    protected virtual void VisitStatement(ProduceStmt pStmt) {
        if (pStmt.Rhss != null) HandleRhsList(pStmt.Rhss);
    }
    
    protected virtual void VisitStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            HandleExpression(ifStmt.Guard);
        } if (IsWorthVisiting(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            HandleBlock(ifStmt.Thn);
        } if (ifStmt.Els != null && IsWorthVisiting(ifStmt.Els.StartToken.pos, ifStmt.Els.EndToken.pos)) {
            if (ifStmt.Els is BlockStmt bEls) {
                HandleBlock(bEls);
            } else if (ifStmt.Els != null) {
                HandleStatement(ifStmt.Els);
            }
        }
    }
    
    protected virtual void VisitStatement(WhileStmt whileStmt) {
        if (IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos)) {
            HandleExpression(whileStmt.Guard);
        }
        if (whileStmt.Body != null) HandleBlock(whileStmt.Body);   
    }

    protected virtual void VisitStatement(ForLoopStmt forStmt) {
        if (IsWorthVisiting(forStmt.Start.StartToken.pos, forStmt.Start.EndToken.pos)) {
            HandleExpression(forStmt.Start);
        } if (IsWorthVisiting(forStmt.End.StartToken.pos, forStmt.End.EndToken.pos)) {
            HandleExpression(forStmt.End);
        }
        HandleBlock(forStmt.Body);
    }

    protected virtual void VisitStatement(ForallStmt forStmt) {
        if (IsWorthVisiting(forStmt.Range.StartToken.pos, forStmt.Range.EndToken.pos)) {
            HandleExpression(forStmt.Range);
        } if (IsWorthVisiting(forStmt.Body.StartToken.pos, forStmt.Body.EndToken.pos)) {
            HandleStatement(forStmt.Body);
        }
    }
    
    protected virtual void VisitStatement(BreakOrContinueStmt bcStmt) { }

    protected virtual void VisitStatement(AlternativeLoopStmt altLStmt) {
        HandleGuardedAlternatives(altLStmt.Alternatives);
    }

    protected virtual void VisitStatement(AlternativeStmt altStmt) {
        HandleGuardedAlternatives(altStmt.Alternatives);
    }
    
    protected virtual void VisitStatement(MatchStmt matchStmt) {
        if (IsWorthVisiting(matchStmt.Source.StartToken.pos, matchStmt.Source.EndToken.pos)) {
            HandleExpression(matchStmt.Source);
        }
        foreach (var cs in matchStmt.Cases) {
            if (!IsWorthVisiting(cs.StartToken.pos, cs.EndToken.pos)) 
                continue;
            HandleBlock(cs.Body);
        }
    }

    protected virtual void VisitStatement(NestedMatchStmt nMatchStmt) {
        if (IsWorthVisiting(nMatchStmt.Source.StartToken.pos, nMatchStmt.Source.EndToken.pos)) {
            HandleExpression(nMatchStmt.Source);
        }
        foreach (var cs in nMatchStmt.Cases) {
            if (!IsWorthVisiting(cs.StartToken.pos, cs.EndToken.pos)) 
                continue;
            HandleBlock(cs.Body);
        }
    }

    protected virtual void VisitStatement(CallStmt callStmt) {
        HandleExprList(callStmt.Lhs);
        if (TargetFound()) return;
        if (IsWorthVisiting(callStmt.OriginalInitialLhs.StartToken.pos, callStmt.EndToken.pos)) {
            HandleExpression(callStmt.OriginalInitialLhs);
        } if (IsWorthVisiting(callStmt.MethodSelect.StartToken.pos, callStmt.MethodSelect.EndToken.pos)) {
            HandleExpression(callStmt.MethodSelect);
        } if (IsWorthVisiting(callStmt.Receiver.StartToken.pos, callStmt.EndToken.pos)) {
            HandleExpression(callStmt.Receiver);
        } if (IsWorthVisiting(callStmt.Method.StartToken.pos, callStmt.Method.EndToken.pos)) {
            HandleMethod(callStmt.Method);
        }
        HandleActualBindings(callStmt.Bindings);
        HandleExprList(callStmt.Args);
    }

    protected virtual void VisitStatement(ModifyStmt mdStmt) {
        if (mdStmt.Body == null) return;
        HandleStatement(mdStmt.Body);
    }

    protected virtual void VisitStatement(HideRevealStmt hRStmt) {
        HandleExprList(hRStmt.Exprs);
    }

    protected virtual void VisitStatement(BlockByProofStmt bBpStmt) {
        HandleStatement(bBpStmt.Body);
    }

    protected virtual void VisitStatement(SkeletonStatement skStmt) {
        if (skStmt.S == null) return;
        HandleStatement(skStmt.S);
    }

    protected virtual void VisitStatement(PrintStmt prtStmt) { }
    
    // statements used specifically in specs
    // by default we don't visit these since we are not mutating them
    protected virtual void VisitStatement(OpaqueBlock opqBlock) { }
    
    // includes AssertStmt, AssumeStmt, ExpectStmt
    protected virtual void VisitStatement(PredicateStmt predStmt) { }
    
    protected virtual void VisitStatement(CalcStmt calcStmt) { }
    
    /// ----------------------------
    /// Group of expression visitors
    /// ----------------------------
    protected virtual void HandleExpression(Expression expr) {
        var derivedType = expr.GetType();
        while (derivedType != typeof(object) && derivedType != null) {
            if (_expressionHandlers.TryGetValue(derivedType, out var handler)) {
                handler(expr);
                return;
            }
            derivedType = derivedType.BaseType;
        }
    }
    
    // no sub-expressions to further visit
    protected virtual void VisitExpression(LiteralExpr litExpr) { }

    protected virtual void VisitExpression(BinaryExpr bExpr) {
        List<Expression> exprs = [bExpr.E0, bExpr.E1];
        HandleExprList(exprs);
    }
    
    protected virtual void VisitExpression(UnaryExpr uExpr) {
        HandleExpression(uExpr.E);
    }
    
    protected virtual void VisitExpression(ParensExpression pExpr) {
        HandleExpression(pExpr.E);
    }
    
    protected virtual void VisitExpression(NegationExpression nExpr) {
        HandleExpression(nExpr.E);
    }

    protected virtual void VisitExpression(ChainingExpression cExpr) {
        if (cExpr.E is BinaryExpr bExpr && bExpr.Op == BinaryExpr.Opcode.And) {
            List<Expression> exprs = [bExpr.E0, bExpr.E1];
            HandleExprList(exprs);
        }
    }
    
    // no sub-expressions to further visit
    protected virtual void VisitExpression(NameSegment nSegExpr) { }

    protected virtual void VisitExpression(LetExpr ltExpr) {
        var exprs = Enumerable.Concat([ltExpr.Body], ltExpr.RHSs).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(LetOrFailExpr ltOrFExpr) {
        HandleExprList([ltOrFExpr.Rhs, ltOrFExpr.Body]);
    }

    protected virtual void VisitExpression(ApplyExpr appExpr) {
        var exprs = Enumerable.Concat([appExpr.Function], appExpr.Args).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(SuffixExpr suffixExpr) {
        HandleExpression(suffixExpr.Lhs);
        if (TargetFound()) return;

        if (suffixExpr is ApplySuffix appSufExpr) {
            HandleActualBindings(appSufExpr.Bindings);
        }
    }

    protected virtual void VisitExpression(FunctionCallExpr fCallExpr) {
        if (IsWorthVisiting(fCallExpr.Receiver.StartToken.pos, fCallExpr.Receiver.EndToken.pos)) {
            HandleExpression(fCallExpr.Receiver);
        }
        HandleActualBindings(fCallExpr.Bindings);
    }
    
    protected virtual void VisitExpression(MemberSelectExpr mSelExpr) {
        HandleExpression(mSelExpr.Obj);
    }

    protected virtual void VisitExpression(ITEExpr iteExpr) {
        List<Expression> exprs = [iteExpr.Test, iteExpr.Thn, iteExpr.Els];
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(MatchExpr mExpr) {
        var cases = mExpr.Cases.Select(e => e.Body);
        var exprs = Enumerable.Concat([mExpr.Source], cases).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(NestedMatchExpr nMExpr) {
        var cases = nMExpr.Cases.Select(e => e.Body);
        var exprs = Enumerable.Concat([nMExpr.Source], cases).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(DisplayExpression dExpr) {
        HandleExprList(dExpr.Elements);
    }

    protected virtual void VisitExpression(MapDisplayExpr mDExpr) {
        var keyElements = mDExpr.Elements.Select(e => e.A).ToList();
        var valueElements = mDExpr.Elements.Select(e => e.B).ToList();
        var exprs = Enumerable.Concat(keyElements, valueElements).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(SeqConstructionExpr seqCExpr) {
        List<Expression> exprs = [seqCExpr.N, seqCExpr.Initializer];
        HandleExprList(exprs);
    }
    
    protected virtual void VisitExpression(MultiSetFormingExpr mSetFExpr) {
        HandleExpression(mSetFExpr.E);
    }

    protected virtual void VisitExpression(SeqSelectExpr seqSExpr) {
        List<Expression> exprs = [seqSExpr.Seq];
        if (seqSExpr.E0 != null) exprs.Add(seqSExpr.E0);
        if (seqSExpr.E1 != null) exprs.Add(seqSExpr.E1);
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(MultiSelectExpr mSExpr) {
        var exprs = Enumerable.Concat([mSExpr.Array], mSExpr.Indices).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(SeqUpdateExpr seqUExpr) {
        List<Expression> exprs = [seqUExpr.Seq, seqUExpr.Index, seqUExpr.Value];
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(ComprehensionExpr compExpr) {
        List<Expression> exprs = [compExpr.Term];
        if (compExpr.Range != null) exprs.Add(compExpr.Range);
        HandleExprList(exprs);
        if (TargetFound()) return;

        if (compExpr is MapComprehension mCompExpr && mCompExpr.TermLeft != null) {
            HandleExpression(mCompExpr.TermLeft);
        }
    }

    protected virtual void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        var updates = dtUExpr.Updates.Select(e => e.Item3);
        var exprs = Enumerable.Concat([dtUExpr.Root], updates).ToList();
        HandleExprList(exprs);
    }

    protected virtual void VisitExpression(DatatypeValue dtValue) {
        HandleActualBindings(dtValue.Bindings);
    }

    protected virtual void VisitExpression(StmtExpr stmtExpr) {
        HandleStatement(stmtExpr.S);
        if (TargetFound()) return;
        HandleExpression(stmtExpr.E); 
    }
    
    protected virtual void VisitExpression(OldExpr oldExpr) { }
    
    protected virtual void VisitExpression(UnchangedExpr unchExpr) { }
    
    protected virtual void VisitExpression(DecreasesToExpr dToExpr) { }
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    protected virtual void HandleExprList(List<Expression> exprs) {
        foreach (var expr in exprs) {
            if (!IsWorthVisiting(expr.StartToken.pos, expr.EndToken.pos)) 
                continue;
            HandleExpression(expr);
        }
    }

    protected virtual void HandleRhsList(List<AssignmentRhs> rhss) {
        foreach (var rhs in rhss) {
            if (!IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos))
                continue;
            HandleAssignmentRhs(rhs);
        }
    }

    protected virtual void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
        } else if (aRhs is TypeRhs tpRhs) {
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

    protected virtual void HandleGuardedAlternatives(List<GuardedAlternative> alternatives) {
        foreach (var alt in alternatives) {
            if (IsWorthVisiting(alt.Guard.StartToken.pos, alt.Guard.EndToken.pos)) {
                HandleExpression(alt.Guard);
            }
            HandleBlock(alt.Body);  
        }
    }

    protected virtual void HandleActualBindings(ActualBindings bindings) {
        foreach (var binding in bindings.ArgumentBindings) {
            if (!IsWorthVisiting(binding.Actual.StartToken.pos, binding.Actual.EndToken.pos))
                continue;
            HandleExpression(binding.Actual);
        }
    }
    
    protected virtual bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        if (int.TryParse(MutationTargetPos, out var position)) {
            return position == -1 || // visit the tree without searching for specific target
                   (tokenStartPos <= position && // specific target
                    position <= tokenEndPos);
        } 
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        return (tokenStartPos <= int.Parse(positions[0]) &&
                int.Parse(positions[1]) <= tokenEndPos);
    }

    protected bool TargetFound() {
        return TargetStatement != null || TargetExpression != null || TargetAssignmentRhs != null;
    }

    private INode? GetTarget() {
        if (TargetExpression != null) return TargetExpression;
        if (TargetStatement != null) return TargetStatement;
        return TargetAssignmentRhs;
    }

    private string GetTargetType() {
        return TargetExpression != null ? "expression" : 
            (TargetStatement != null ? "statement" : "assignment rhs");
    }
}