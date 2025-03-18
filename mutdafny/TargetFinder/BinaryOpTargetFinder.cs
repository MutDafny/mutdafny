using Microsoft.Dafny;
using Type = System.Type;

namespace MutDafny.TargetFinder;

// this type of finder is used to find the statement in which specific binary operators are used
public class BinaryOpTargetFinder : TargetFinder
{
    private readonly int _mutationTargetPos;
    private readonly Dictionary<Type, Action<Statement>> _statementHandlers;
    private readonly Dictionary<Type, Action<Expression>> _expressionHandlers;
    
    public BinaryOpTargetFinder(int mutationTargetPos, ErrorReporter reporter)
        : base(mutationTargetPos, reporter)
    {
        _mutationTargetPos = mutationTargetPos;
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
            {typeof(ReturnStmt), stmt => VisitStatement((stmt as ProduceStmt)!)},
            {typeof(YieldStmt), stmt => VisitStatement((stmt as ProduceStmt)!)},
            {typeof(IfStmt), stmt => VisitStatement((stmt as IfStmt)!)},
            {typeof(WhileStmt), stmt => VisitStatement((stmt as WhileStmt)!)},
            {typeof(ForLoopStmt), stmt => VisitStatement((stmt as ForLoopStmt)!)},
            {typeof(ForallStmt), stmt => VisitStatement((stmt as ForallStmt)!)},
            {typeof(AlternativeLoopStmt), stmt => VisitStatement((stmt as AlternativeLoopStmt)!)},
            {typeof(AlternativeStmt), stmt => VisitStatement((stmt as AlternativeStmt)!)},
            {typeof(MatchStmt), stmt => VisitStatement((stmt as MatchStmt)!)},
            {typeof(NestedMatchStmt), stmt => VisitStatement((stmt as NestedMatchStmt)!)},
            {typeof(CallStmt), stmt => VisitStatement((stmt as CallStmt)!)},
            {typeof(ModifyStmt), stmt => VisitStatement((stmt as ModifyStmt)!)},
            {typeof(HideRevealStmt), stmt => VisitStatement((stmt as HideRevealStmt)!)},
            {typeof(BlockByProofStmt), stmt => VisitStatement((stmt as BlockByProofStmt)!)},
            {typeof(SkeletonStatement), stmt => VisitStatement((stmt as SkeletonStatement)!)},
        };
        _expressionHandlers = new Dictionary<Type, Action<Expression>> {
            {typeof(BinaryExpr), expr => VisitExpression((expr as BinaryExpr)!)},
            {typeof(UnaryExpr), expr => HandleExpression(((expr as UnaryExpr)!).E)},
            {typeof(ParensExpression), expr => HandleExpression(((expr as ParensExpression)!).E)},
            {typeof(NegationExpression), expr => HandleExpression(((expr as NegationExpression)!).E)},
            {typeof(ChainingExpression), expr => HandleExpression(((expr as ChainingExpression)!).E)},
            {typeof(LetExpr), expr => VisitExpression((expr as LetExpr)!)},
            {typeof(LetOrFailExpr), expr => VisitExpression((expr as LetOrFailExpr)!)},
            {typeof(ApplyExpr), expr => VisitExpression((expr as ApplyExpr)!)},
            {typeof(SuffixExpr), expr => VisitExpression((expr as SuffixExpr)!)},
            {typeof(ApplySuffix), expr => VisitExpression((expr as SuffixExpr)!)},
            {typeof(FunctionCallExpr), expr => VisitExpression((expr as FunctionCallExpr)!)},
            {typeof(MemberSelectExpr), expr => HandleExpression(((expr as MemberSelectExpr)!).Obj)},
            {typeof(ITEExpr), expr => VisitExpression((expr as ITEExpr)!)},
            {typeof(MatchExpr), expr => VisitExpression((expr as MatchExpr)!)},
            {typeof(NestedMatchExpr), expr => VisitExpression((expr as NestedMatchExpr)!)},
            {typeof(DisplayExpression), expr => VisitExpression((expr as DisplayExpression)!)},
            {typeof(MapDisplayExpr), expr => VisitExpression((expr as MapDisplayExpr)!)},
            {typeof(SeqConstructionExpr), expr => VisitExpression((expr as SeqConstructionExpr)!)},
            {typeof(MultiSetFormingExpr), expr => HandleExpression(((expr as MultiSetFormingExpr)!).E)},
            {typeof(SeqSelectExpr), expr => VisitExpression((expr as SeqSelectExpr)!)},
            {typeof(MultiSelectExpr), expr => VisitExpression((expr as MultiSelectExpr)!)},
            {typeof(SeqUpdateExpr), expr => VisitExpression((expr as SeqUpdateExpr)!)},    
            {typeof(ComprehensionExpr), expr => VisitExpression((expr as ComprehensionExpr)!)},
            {typeof(MapComprehension), expr => VisitExpression((expr as ComprehensionExpr)!)},
            {typeof(DatatypeUpdateExpr), expr => VisitExpression((expr as DatatypeUpdateExpr)!)},
            {typeof(DatatypeValue), expr => VisitExpression((expr as DatatypeValue)!)},
            {typeof(StmtExpr), expr => VisitExpression((expr as StmtExpr)!)},
        };
    }

    /// ---------------------------
    /// Group of statement visitors
    /// ---------------------------
    protected override void HandleStatement(Statement stmt) {
        if (_statementHandlers.TryGetValue(stmt.GetType(), out var handler)) {
            handler(stmt);
        }
    }
    
    private void VisitStatement(BlockStmt blockStmt) {
        HandleBlock(blockStmt);
        if (TargetFound()) return;
        
        if (blockStmt is DividedBlockStmt dBlockStmt) {
            HandleBlock(dBlockStmt.BodyInit);
            if (TargetFound()) return;
            HandleBlock(dBlockStmt.BodyProper);
        }
    }

    private void VisitStatement(ConcreteAssignStatement cAStmt) {
        HandleExprList(cAStmt.Lhss);
    }

    private void VisitStatement(AssignStatement aStmt) {
        VisitStatement(aStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleRhsList(aStmt.Rhss); 
        if (TargetFound() || aStmt.OriginalInitialLhs == null) 
            return;
        HandleExpression(aStmt.OriginalInitialLhs);
    }
    
    private void VisitStatement(AssignSuchThatStmt aStStmt) {
        VisitStatement(aStStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleExpression(aStStmt.Expr);
    }
    
    private void VisitStatement(AssignOrReturnStmt aOrRStmt) {
        VisitStatement(aOrRStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleExpression(aOrRStmt.Rhs.Expr);
        if (TargetFound()) return;
        HandleRhsList(aOrRStmt.Rhss);
    }

    private void VisitStatement(SingleAssignStmt sAStmt) {
        if (IsWorthVisiting(sAStmt.Lhs.StartToken.pos, sAStmt.Lhs.EndToken.pos)) {
            HandleExpression(sAStmt.Lhs);
        } else {
            HandleRhsList([sAStmt.Rhs]);
        }
    }

    private void VisitStatement(VarDeclStmt vDeclStmt) {
        if (vDeclStmt.Assign == null) return;
        HandleStatement(vDeclStmt.Assign);
    }

    private void VisitStatement(VarDeclPattern vDeclPStmt) {
        HandleExpression(vDeclPStmt.RHS);
    }

    // includes ReturnStmt and YieldStmt
    private void VisitStatement(ProduceStmt pStmt) {
        HandleRhsList(pStmt.Rhss);
    }
    
    private void VisitStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && 
            IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            HandleExpression(ifStmt.Guard);
        } else if (IsWorthVisiting(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            HandleBlock(ifStmt.Thn);
        } else if (ifStmt.Els is BlockStmt bEls) {
            HandleBlock(bEls);
        } else if (ifStmt.Els != null) {
            HandleStatement(ifStmt.Els);
        }
    }
    
    private void VisitStatement(WhileStmt whileStmt) {
        if (IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos)) {
            HandleExpression(whileStmt.Guard);
        } else {
            HandleBlock(whileStmt.Body);   
        }
    }

    private void VisitStatement(ForLoopStmt forStmt) {
        if (IsWorthVisiting(forStmt.Start.StartToken.pos, forStmt.Start.EndToken.pos)) {
            HandleExpression(forStmt.Start);
        } else if (IsWorthVisiting(forStmt.End.StartToken.pos, forStmt.End.EndToken.pos)) {
            HandleExpression(forStmt.End);
        } else {
            HandleBlock(forStmt.Body);
        }
    }

    private void VisitStatement(ForallStmt forStmt) {
        if (IsWorthVisiting(forStmt.Range.StartToken.pos, forStmt.Range.EndToken.pos)) {
            HandleExpression(forStmt.Range);
        } else {
            HandleStatement(forStmt.Body);
        }
    }

    private void VisitStatement(AlternativeLoopStmt altLStmt) {
        HandleGuardedAlternatives(altLStmt.Alternatives);
    }

    private void VisitStatement(AlternativeStmt altStmt) {
        HandleGuardedAlternatives(altStmt.Alternatives);
    }
    
    private void VisitStatement(MatchStmt matchStmt) {
        if (IsWorthVisiting(matchStmt.Source.StartToken.pos, matchStmt.Source.EndToken.pos)) {
            HandleExpression(matchStmt.Source);
            return;
        }
        foreach (var cs in matchStmt.Cases) {
            if (!IsWorthVisiting(cs.StartToken.pos, cs.EndToken.pos)) continue;
            HandleBlock(cs.Body); break;
        }
    }

    private void VisitStatement(NestedMatchStmt nMatchStmt) {
        if (IsWorthVisiting(nMatchStmt.Source.StartToken.pos, nMatchStmt.Source.EndToken.pos)) {
            HandleExpression(nMatchStmt.Source);
            return;
        }
        foreach (var cs in nMatchStmt.Cases) {
            if (!IsWorthVisiting(cs.StartToken.pos, cs.EndToken.pos)) continue;
            HandleBlock(cs.Body); break;
        }
    }

    private void VisitStatement(CallStmt callStmt) {
        HandleExprList(callStmt.Lhs);
        if (TargetFound()) return;
        if (IsWorthVisiting(callStmt.OriginalInitialLhs.StartToken.pos, callStmt.EndToken.pos)) {
            HandleExpression(callStmt.OriginalInitialLhs);
        } else if (IsWorthVisiting(callStmt.MethodSelect.StartToken.pos, callStmt.MethodSelect.EndToken.pos)) {
            HandleExpression(callStmt.MethodSelect);
        } else if (IsWorthVisiting(callStmt.Receiver.StartToken.pos, callStmt.EndToken.pos)) {
            HandleExpression(callStmt.Receiver);
        } else if (IsWorthVisiting(callStmt.Method.StartToken.pos, callStmt.Method.EndToken.pos)) {
            HandleMethod(callStmt.Method);
        } else if (IsWorthVisiting(callStmt.Bindings.StartToken.pos, callStmt.Bindings.EndToken.pos)) {
            HandleActualBindings(callStmt.Bindings);
        } else {
            HandleExprList(callStmt.Args);
        }
    }

    private void VisitStatement(ModifyStmt mdStmt) {
        HandleStatement(mdStmt.Body);
    }

    private void VisitStatement(HideRevealStmt hRStmt) {
        HandleExprList(hRStmt.Exprs);
    }

    private void VisitStatement(BlockByProofStmt bBpStmt) {
        HandleStatement(bBpStmt.Body);
    }

    private void VisitStatement(SkeletonStatement skStmt) {
        if (skStmt.S == null) return;
        HandleStatement(skStmt.S);
    }

    /// ----------------------------
    /// Group of expression visitors
    /// ----------------------------
    protected override void HandleExpression(Expression expr) {
        if (_expressionHandlers.TryGetValue(expr.GetType(), out var handler)) {
            handler(expr);
        }
    }

    private void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            TargetExpression = bExpr;
            return;
        }
        HandleExpression(bExpr.E0);
        if (TargetFound()) return;
        HandleExpression(bExpr.E1);
    }

    private void VisitExpression(LetExpr ltExpr) {
        var exprs = Enumerable.Concat([ltExpr.Body], ltExpr.RHSs).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(LetOrFailExpr ltOrFExpr) {
        HandleExprList([ltOrFExpr.Rhs, ltOrFExpr.Body]);
    }

    private void VisitExpression(ApplyExpr appExpr) {
        var exprs = Enumerable.Concat([appExpr.Function], appExpr.Args).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(SuffixExpr suffixExpr) {
        HandleExpression(suffixExpr.Lhs);
        if (TargetFound()) return;

        if (suffixExpr is ApplySuffix appSufExpr) {
            HandleActualBindings(appSufExpr.Bindings);
        }
    }

    private void VisitExpression(FunctionCallExpr fCallExpr) {
        if (IsWorthVisiting(fCallExpr.Receiver.StartToken.pos, fCallExpr.Receiver.EndToken.pos)) {
            HandleExpression(fCallExpr.Receiver);
        } else {
            HandleActualBindings(fCallExpr.Bindings);
        }
    }

    private void VisitExpression(ITEExpr iteExpr) {
        List<Expression> exprs = [iteExpr.Test, iteExpr.Thn, iteExpr.Els];
        HandleExprList(exprs);
    }

    private void VisitExpression(MatchExpr mExpr) {
        var cases = mExpr.Cases.Select(e => e.Body);
        var exprs = Enumerable.Concat([mExpr.Source], cases).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(NestedMatchExpr nMExpr) {
        var cases = nMExpr.Cases.Select(e => e.Body);
        var exprs = Enumerable.Concat([nMExpr.Source], cases).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(DisplayExpression dExpr) {
        HandleExprList(dExpr.Elements);
    }

    private void VisitExpression(MapDisplayExpr mDExpr) {
        var keyElements = mDExpr.Elements.Select(e => e.A).ToList();
        var valueElements = mDExpr.Elements.Select(e => e.B).ToList();
        var exprs = Enumerable.Concat(keyElements, valueElements).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(SeqConstructionExpr seqCExpr) {
        List<Expression> exprs = [seqCExpr.N, seqCExpr.Initializer];
        HandleExprList(exprs);
    }

    private void VisitExpression(SeqSelectExpr seqSExpr) {
        List<Expression> exprs = [seqSExpr.Seq];
        if (seqSExpr.E0 != null) exprs.Add(seqSExpr.E0);
        if (seqSExpr.E1 != null) exprs.Add(seqSExpr.E1);
        HandleExprList(exprs);
    }

    private void VisitExpression(MultiSelectExpr mSExpr) {
        var exprs = Enumerable.Concat([mSExpr.Array], mSExpr.Indices).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(SeqUpdateExpr seqUExpr) {
        List<Expression> exprs = [seqUExpr.Seq, seqUExpr.Index, seqUExpr.Value];
        HandleExprList(exprs);
    }

    private void VisitExpression(ComprehensionExpr compExpr) {
        List<Expression> exprs = [compExpr.Term];
        if (compExpr.Range != null) exprs.Add(compExpr.Range);
        HandleExprList(exprs);
        if (TargetFound()) return;

        if (compExpr is MapComprehension mComExpr) {
            HandleExpression(mComExpr.TermLeft);
        }
    }

    private void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        var updates = dtUExpr.Updates.Select(e => e.Item3);
        var exprs = Enumerable.Concat([dtUExpr.Root], updates).ToList();
        HandleExprList(exprs);
    }

    private void VisitExpression(DatatypeValue dtValue) {
        HandleActualBindings(dtValue.Bindings);
    }

    private void VisitExpression(StmtExpr stmtExpr) {
        HandleStatement(stmtExpr.S);
        if (TargetFound()) return;
        HandleExpression(stmtExpr.E); 
    }
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    private void HandleExprList(List<Expression> exprs) {
        foreach (var expr in exprs) {
            if (!IsWorthVisiting(expr.StartToken.pos, expr.EndToken.pos)) 
                continue;
            HandleExpression(expr); break;
        }
    }

    private void HandleRhsList(List<AssignmentRhs> rhss) {
        foreach (var rhs in rhss) {
            if (!IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos))
                continue;
            HandleAssignmentRhs(rhs);
        }
    }

    private void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
        } else if (aRhs is TypeRhs tpRhs) {
            if (IsWorthVisiting(tpRhs.ElementInit.StartToken.pos, tpRhs.ElementInit.EndToken.pos)) {
                HandleExpression(tpRhs.ElementInit);
            } else if (IsWorthVisiting(tpRhs.Bindings.StartToken.pos, tpRhs.Bindings.EndToken.pos)) {
                HandleActualBindings(tpRhs.Bindings);
            } else {
                HandleExprList(tpRhs.InitDisplay);   
            }
        }
    }

    private void HandleGuardedAlternatives(List<GuardedAlternative> alternatives) {
        foreach (var alt in alternatives) {
            if (IsWorthVisiting(alt.Guard.StartToken.pos, alt.Guard.EndToken.pos)) {
                HandleExpression(alt.Guard);
            } else {
                HandleBlock(alt.Body);  
            }
        }
    }

    private void HandleActualBindings(ActualBindings bindings) {
        foreach (var binding in bindings.ArgumentBindings) {
            if (!IsWorthVisiting(binding.Actual.StartToken.pos, binding.Actual.EndToken.pos))
                continue;
            HandleExpression(binding.Actual); break;
        }
    }
    
    private bool IsTarget(BinaryExpr expr) {
        return expr.Center.pos == _mutationTargetPos;
    }
}