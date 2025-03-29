using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator replaces a relational or conditional binary expression with true or false
public class BinaryOpBoolMutator(int mutationTargetPos, string val, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            TargetExpression = bExpr;
            return;
        }
        
        HandleExpression(bExpr.E0);
        if (TargetFound()) { // mutate
            bExpr.E0 = CreateMutatedExpression(bExpr.E0);
            return;
        }
        HandleExpression(bExpr.E1);
        if (TargetFound()) // mutate
            bExpr.E1 = CreateMutatedExpression(bExpr.E1);
    }
    
    private bool IsTarget(BinaryExpr expr) {
        return expr.Center.pos == MutationTargetPos;
    }

    private LiteralExpr CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        return new LiteralExpr(originalExpr.Origin, bool.Parse(val));
    }
    
    /// ---------------------------
    /// Group of statement visitors
    /// ---------------------------
    protected override void VisitStatement(AssignStatement aStmt) {
        VisitStatement(aStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleRhsList(aStmt.Rhss); 
        if (TargetFound() || aStmt.OriginalInitialLhs == null) 
            return;
        HandleExpression(aStmt.OriginalInitialLhs);
        if (TargetFound()) // mutate
            aStmt.OriginalInitialLhs = CreateMutatedExpression(aStmt.OriginalInitialLhs);
    }

    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        VisitStatement(aStStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleExpression(aStStmt.Expr);
        if (TargetFound()) // mutate
            aStStmt.Expr = CreateMutatedExpression(aStStmt.Expr);
    }

    protected override void VisitStatement(AssignOrReturnStmt aOrRStmt) {
        VisitStatement(aOrRStmt as ConcreteAssignStatement);
        if (TargetFound()) return;
        HandleExpression(aOrRStmt.Rhs.Expr);
        if (TargetFound()) // mutate
            aOrRStmt.Rhs.Expr = CreateMutatedExpression(aOrRStmt.Rhs.Expr);
        HandleRhsList(aOrRStmt.Rhss);
    }
    
    protected override void VisitStatement(SingleAssignStmt sAStmt) {
        if (IsWorthVisiting(sAStmt.Lhs.StartToken.pos, sAStmt.Lhs.EndToken.pos)) {
            HandleExpression(sAStmt.Lhs);
            if (TargetFound()) // mutate
                sAStmt.Lhs = CreateMutatedExpression(sAStmt.Lhs);
        } 
        HandleRhsList([sAStmt.Rhs]);
    }
    
    protected override void VisitStatement(VarDeclPattern vDeclPStmt) {
        HandleExpression(vDeclPStmt.RHS);
        if (TargetFound()) // mutate
            vDeclPStmt.RHS = CreateMutatedExpression(vDeclPStmt.RHS);
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            HandleExpression(ifStmt.Guard);
            if (TargetFound()) // mutate
                ifStmt.Guard = CreateMutatedExpression(ifStmt.Guard);
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
    
    protected override void VisitStatement(WhileStmt whileStmt) {
        if (IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos)) {
            HandleExpression(whileStmt.Guard);
            if (TargetFound()) // mutate
                whileStmt.Guard = CreateMutatedExpression(whileStmt.Guard);
        }
        if (whileStmt.Body != null) HandleBlock(whileStmt.Body); 
    }
    
    protected override void VisitStatement(ForLoopStmt forStmt) {
        if (IsWorthVisiting(forStmt.Start.StartToken.pos, forStmt.Start.EndToken.pos)) {
            HandleExpression(forStmt.Start);
            if (TargetFound()) // mutate
                forStmt.Start = CreateMutatedExpression(forStmt.Start);
        } if (IsWorthVisiting(forStmt.End.StartToken.pos, forStmt.End.EndToken.pos)) {
            HandleExpression(forStmt.End);
            if (TargetFound()) // mutate
                forStmt.End = CreateMutatedExpression(forStmt.End);
        }
        HandleBlock(forStmt.Body);
    }
    
    protected override void VisitStatement(ForallStmt forStmt) {
        if (IsWorthVisiting(forStmt.Range.StartToken.pos, forStmt.Range.EndToken.pos)) {
            HandleExpression(forStmt.Range);
            if (TargetFound()) // mutate
                forStmt.Range = CreateMutatedExpression(forStmt.Range);
        } if (IsWorthVisiting(forStmt.Body.StartToken.pos, forStmt.Body.EndToken.pos)) {
            HandleStatement(forStmt.Body);
        }
    }
    
    protected override void VisitStatement(MatchStmt matchStmt) {
        // if (IsWorthVisiting(matchStmt.Source.StartToken.pos, matchStmt.Source.EndToken.pos)) {
        //     HandleExpression(matchStmt.Source);
        //     if (TargetFound()) // mutate
        //         matchStmt.Source = CreateMutatedExpression(matchStmt.Source); // Dafny doesn't define setter
        // }
        foreach (var cs in matchStmt.Cases) {
            if (!IsWorthVisiting(cs.StartToken.pos, cs.EndToken.pos)) 
                continue;
            HandleBlock(cs.Body);
        }
    }
    
    protected override void VisitStatement(NestedMatchStmt nMatchStmt) {
        // if (IsWorthVisiting(nMatchStmt.Source.StartToken.pos, nMatchStmt.Source.EndToken.pos)) {
        //     HandleExpression(nMatchStmt.Source);
        //     if (TargetFound()) // mutate
        //         nMatchStmt.Source = CreateMutatedExpression(nMatchStmt.Source); // Dafny doesn't define setter
        // }
        foreach (var cs in nMatchStmt.Cases) {
            if (!IsWorthVisiting(cs.StartToken.pos, cs.EndToken.pos)) 
                continue;
            HandleBlock(cs.Body);
        }
    }
    
    protected override void VisitStatement(CallStmt callStmt) {
        HandleExprList(callStmt.Lhs);
        if (TargetFound()) return;
        if (IsWorthVisiting(callStmt.OriginalInitialLhs.StartToken.pos, callStmt.EndToken.pos)) {
            HandleExpression(callStmt.OriginalInitialLhs);
            if (TargetFound()) // mutate
                callStmt.OriginalInitialLhs = CreateMutatedExpression(callStmt.OriginalInitialLhs);
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

    /// ----------------------------
    /// Group of expression visitors
    /// ----------------------------
    protected override void VisitExpression(UnaryExpr uExpr) {
        HandleExpression(uExpr.E);
        if (TargetFound()) // mutate
            uExpr.E = CreateMutatedExpression(uExpr.E);
    }
    
    protected override void VisitExpression(ParensExpression pExpr) {
        HandleExpression(pExpr.E);
        if (TargetFound()) // mutate
            pExpr.E = CreateMutatedExpression(pExpr.E);
    }
        
    protected override void VisitExpression(NegationExpression nExpr) {
        HandleExpression(nExpr.E);
        if (TargetFound()) // mutate
            nExpr.E = CreateMutatedExpression(nExpr.E);
    }
    
    // chaining expressions don't support this kind of mutation
    protected override void VisitExpression(ChainingExpression cExpr) { }
    
    protected override void VisitExpression(LetExpr ltExpr) {
        HandleExpression(ltExpr.Body);
        if (TargetFound()) // mutate
            ltExpr.Body = CreateMutatedExpression(ltExpr.Body);
        HandleExprList(ltExpr.RHSs);
    }
    
    protected override void VisitExpression(LetOrFailExpr ltOrFExpr) {
        HandleExpression(ltOrFExpr.Rhs);
        if (TargetFound()) { // mutate
            ltOrFExpr.Rhs = CreateMutatedExpression(ltOrFExpr.Rhs);
            return;
        }
        HandleExpression(ltOrFExpr.Body);
        if (TargetFound()) // mutate
            ltOrFExpr.Body = CreateMutatedExpression(ltOrFExpr.Body);
    }
    
    protected override void VisitExpression(ApplyExpr appExpr) {
        HandleExpression(appExpr.Function);
        if (TargetFound()) // mutate
            appExpr.Function = CreateMutatedExpression(appExpr.Function);
        HandleExprList(appExpr.Args);
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        HandleExpression(suffixExpr.Lhs);
        if (TargetFound()) // mutate
            suffixExpr.Lhs = CreateMutatedExpression(suffixExpr.Lhs);

        if (suffixExpr is ApplySuffix appSufExpr) {
            HandleActualBindings(appSufExpr.Bindings);
        }
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        if (IsWorthVisiting(fCallExpr.Receiver.StartToken.pos, fCallExpr.Receiver.EndToken.pos)) {
            HandleExpression(fCallExpr.Receiver);
            if (TargetFound()) // mutate
                fCallExpr.Receiver = CreateMutatedExpression(fCallExpr.Receiver);
        }
        HandleActualBindings(fCallExpr.Bindings);
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        HandleExpression(mSelExpr.Obj);
        if (TargetFound()) // mutate
            mSelExpr.Obj = CreateMutatedExpression(mSelExpr.Obj);
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {      
        HandleExpression(iteExpr.Test);
        if (TargetFound()) { // mutate
            iteExpr.Test = CreateMutatedExpression(iteExpr.Test);
            return;
        }
        HandleExpression(iteExpr.Thn);
        if (TargetFound()) { // mutate
            iteExpr.Thn = CreateMutatedExpression(iteExpr.Thn);
            return;
        }
        HandleExpression(iteExpr.Els);
        if (TargetFound()) // mutate
            iteExpr.Els = CreateMutatedExpression(iteExpr.Els);
    }
    
    protected override void VisitExpression(MatchExpr mExpr) {
        // foreach (var c in mExpr.Cases) {
        //     HandleExpression(c.Body);
        //     if (TargetFound()) // mutate
        //         c.Body = CreateMutatedExpression(c.Body); // Dafny doesn't define setter
        // }
        //
        // HandleExpression(mExpr.Source);
        // if (TargetFound()) // mutate
        //     mExpr.Source = CreateMutatedExpression(mExpr.Source); // Dafny doesn't define setter
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        foreach (var c in nMExpr.Cases) {
            HandleExpression(c.Body);
            if (TargetFound()) { // mutate
                c.Body = CreateMutatedExpression(c.Body);
                return;
            }
        }
        
        // HandleExpression(nMExpr.Source);
        // if (TargetFound()) // mutate
        //     nMExpr.Source = CreateMutatedExpression(nMExpr.Source); // Dafny doesn't define setter
    }
    
    protected override void VisitExpression(MapDisplayExpr mDExpr) {
        foreach (var elem in mDExpr.Elements)
        {
            HandleExpression(elem.A);
            if (TargetFound()) { // mutate
                elem.A = CreateMutatedExpression(elem.A);
                return;
            }
            HandleExpression(elem.B);
            if (TargetFound()) { // mutate
                elem.B = CreateMutatedExpression(elem.B);
                return;
            }
        }
    }

    protected override void VisitExpression(SeqConstructionExpr seqCExpr)
    {
        HandleExpression(seqCExpr.N);
        if (TargetFound()) { // mutate
            seqCExpr.N = CreateMutatedExpression(seqCExpr.N);
            return;
        }
        HandleExpression(seqCExpr.Initializer);
        if (TargetFound()) // mutate
            seqCExpr.Initializer = CreateMutatedExpression(seqCExpr.Initializer);
    }
    
    protected override void VisitExpression(MultiSetFormingExpr mSetFExpr) { 
        HandleExpression(mSetFExpr.E);
        if (TargetFound()) // mutate
            mSetFExpr.E = CreateMutatedExpression(mSetFExpr.E);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        HandleExpression(seqSExpr.Seq);
        if (TargetFound()) { // mutate
            seqSExpr.Seq = CreateMutatedExpression(seqSExpr.Seq);
            return;
        }
        if (seqSExpr.E0 != null) {
            HandleExpression(seqSExpr.E0);
            if (TargetFound()) { // mutate
                seqSExpr.E0 = CreateMutatedExpression(seqSExpr.E0);
                return;
            }
        }
        if (seqSExpr.E1 != null) {
            HandleExpression(seqSExpr.E1);
            if (TargetFound()) // mutate
                seqSExpr.E1 = CreateMutatedExpression(seqSExpr.E1);
        }
    }
    
    protected override void VisitExpression(MultiSelectExpr mSExpr) {
        HandleExpression(mSExpr.Array);
        if (TargetFound()) // mutate
            mSExpr.Array = CreateMutatedExpression(mSExpr.Array);
        HandleExprList(mSExpr.Indices);
    }
    
    protected override void VisitExpression(SeqUpdateExpr seqUExpr) {
        HandleExpression(seqUExpr.Seq);
        if (TargetFound()) { // mutate
            seqUExpr.Seq = CreateMutatedExpression(seqUExpr.Seq);
            return;
        }
        HandleExpression(seqUExpr.Index);
        if (TargetFound()) { // mutate
            seqUExpr.Index = CreateMutatedExpression(seqUExpr.Index);
            return;
        }
        HandleExpression(seqUExpr.Value);
        if (TargetFound()) // mutate
            seqUExpr.Value = CreateMutatedExpression(seqUExpr.Value);
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        HandleExpression(compExpr.Term);
        if (TargetFound()) { // mutate
            compExpr.Term = CreateMutatedExpression(compExpr.Term);
            return;
        }
        if (compExpr.Range != null) {
            HandleExpression(compExpr.Range);
            if (TargetFound()) { // mutate
                compExpr.Range = CreateMutatedExpression(compExpr.Range);
                return;
            }
        }

        if (compExpr is MapComprehension mCompExpr && mCompExpr.TermLeft != null) {
            HandleExpression(mCompExpr.TermLeft);
            if (TargetFound()) // mutate
                mCompExpr.TermLeft = CreateMutatedExpression(mCompExpr.TermLeft);
        }
    }
    
    protected override void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        HandleExpression(dtUExpr.Root);
        if (TargetFound()) { // mutate
            dtUExpr.Root = CreateMutatedExpression(dtUExpr.Root);
            return;
        }

        for (var i = 0; i < dtUExpr.Updates.Count; i++) {
            var update = dtUExpr.Updates[i];
            HandleExpression(update.Item3);
            if (TargetFound()) { // mutate
                var newItem = CreateMutatedExpression(update.Item3);
                var newUpdate = Tuple.Create(update.Item1, update.Item2, newItem as Expression);
                dtUExpr.Updates[i] = newUpdate;
                return;
            }
        }
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        HandleStatement(stmtExpr.S);
        if (TargetFound()) return;
        HandleExpression(stmtExpr.E);
        if (TargetFound()) // mutate
            stmtExpr.E = CreateMutatedExpression(stmtExpr.E);
    }
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    protected override void HandleExprList(List<Expression> exprs) {
        foreach (var expr in exprs) {
            if (!IsWorthVisiting(expr.StartToken.pos, expr.EndToken.pos)) 
                continue;
            HandleExpression(expr);
            if (TargetFound()) { // mutate
                var newExpr = CreateMutatedExpression(expr);
                var i = exprs.FindIndex(e => e == expr);
                exprs[i] = newExpr;
                return;
            }
        }
    }
    
    protected override void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
            if (TargetFound()) // mutate
                exprRhs.Expr = CreateMutatedExpression(exprRhs.Expr);
        } else if (aRhs is TypeRhs tpRhs) {
            var elInit = tpRhs.ElementInit;
            
            if (tpRhs.ArrayDimensions != null) {
                HandleExprList(tpRhs.ArrayDimensions);
            } if (elInit != null && IsWorthVisiting(elInit.StartToken.pos, elInit.EndToken.pos)) {
                HandleExpression(elInit);
                if (TargetFound()) // mutate
                    tpRhs.ElementInit = CreateMutatedExpression(tpRhs.ElementInit);
            } if (tpRhs.InitDisplay != null) {
                HandleExprList(tpRhs.InitDisplay);
            } if (tpRhs.Bindings != null) {
                HandleActualBindings(tpRhs.Bindings);
            }
        }
    }
    
    protected override void HandleGuardedAlternatives(List<GuardedAlternative> alternatives) {
        foreach (var alt in alternatives) {
            if (IsWorthVisiting(alt.Guard.StartToken.pos, alt.Guard.EndToken.pos)) {
                HandleExpression(alt.Guard);
                if (TargetFound()) // mutate
                    alt.Guard = CreateMutatedExpression(alt.Guard);
            }
            HandleBlock(alt.Body);  
        }
    }
    
    protected override void HandleActualBindings(ActualBindings bindings) {
        foreach (var binding in bindings.ArgumentBindings) {
            if (!IsWorthVisiting(binding.Actual.StartToken.pos, binding.Actual.EndToken.pos))
                continue;
            HandleExpression(binding.Actual);
            if (TargetFound()) // mutate
                binding.Actual = CreateMutatedExpression(binding.Actual);
        }
    }
}