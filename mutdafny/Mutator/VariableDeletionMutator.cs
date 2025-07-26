using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

public class VariableDeletionMutator(string mutationTargetPos, string var, ErrorReporter reporter) : Mutator(mutationTargetPos, reporter)
{
    private readonly List<Statement> _toDelete = [];
    private Type? _currentTypeRestriction;
    private string _currentScope = "-";
    private readonly List<(string, string)> _sideEffectVarsToDelete = [];

    private Expression? HandleTarget() {
        Expression? replacementExpr = null;
        if (TargetExpression is BinaryExpr bExpr) {
            if (bExpr.E0 == null && bExpr.E1 != null) {
                replacementExpr = bExpr.E1;
            } else if (bExpr.E0 != null && bExpr.E1 == null) {
                replacementExpr = bExpr.E0;
            }
        }

        if (replacementExpr != null && _currentTypeRestriction != null &&
            replacementExpr.GetType() != _currentTypeRestriction.GetType()) 
        {
            replacementExpr = null;
        }
        TargetExpression = null;
        return replacementExpr;
    }
    
    private Expression? HandleTarget(Statement context) {
        Expression? replacementExpr = null;
        if (TargetExpression is BinaryExpr bExpr) {
            if (bExpr.E0 == null && bExpr.E1 == null) {
                _toDelete.Add(context);
            } else if (bExpr.E0 == null) {
                replacementExpr = bExpr.E1;
            } else {
                replacementExpr = bExpr.E0;
            }
        } else {
            _toDelete.Add(context);
        }

        if (replacementExpr != null && _currentTypeRestriction != null &&
            replacementExpr.GetType() != _currentTypeRestriction.GetType())
        {
            replacementExpr = null;
            _toDelete.Add(context);
        }
        TargetExpression = null;
        return replacementExpr;
    }

    private Expression? HandleTarget(Expression context) {
        Expression? replacementExpr = null;
        if (TargetExpression is BinaryExpr bExpr) {
            if (bExpr.E0 == null && bExpr.E1 != null) {
                replacementExpr = bExpr.E1;
            } else if (bExpr.E0 != null && bExpr.E1 == null) {
                replacementExpr = bExpr.E0;
            }
        }

        if (replacementExpr != null && _currentTypeRestriction != null &&
            replacementExpr.GetType() != _currentTypeRestriction.GetType()) 
        {
            replacementExpr = null;
        }
        TargetExpression = replacementExpr == null ? context : null;
        return replacementExpr;
    }
    
    private bool IsTarget(string name, int tokenPos) {
        var positions = MutationTargetPos.Split("-");
        var currentPositions = _currentScope.Split("-");

        return name == var && 
               (MutationTargetPos == "-" || 
               (int.Parse(positions[0]) == int.Parse(currentPositions[0]) && 
                int.Parse(positions[1]) == int.Parse(currentPositions[1])));
    }
    
    private bool IsTarget(ConstantField cf) {
        return MutationTargetPos == "-" && cf.Name == var;
    }
    
    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsTarget(nSegExpr.Name, nSegExpr.Center.pos)) {
            TargetExpression = nSegExpr;
        }
    }
    
    /// ---------------------------
    /// Group of top level visitors
    /// ---------------------------
    protected override  void HandleSourceDecls(ModuleDefinition module) {
        foreach (var decl in module.SourceDecls.ToList()) {
            // only visit declarations that may contain the mutation target
            if (!IsWorthVisiting(decl.StartToken.pos, decl.EndToken.pos)) continue;
            if (decl is TopLevelDeclWithMembers declWithMembers) { // includes class, trait, datatype, etc.
                HandleMemberDecls(declWithMembers);   
            }
            if (decl is IteratorDecl itDecl) {
                HandleBlock(itDecl.Body);
            } else if (decl is NewtypeDecl newTpDecl) {
                HandleExpression(newTpDecl.Constraint);
                if (TargetFound()) // mutate
                    module.SourceDecls.Remove(decl);
            } else if (decl is SubsetTypeDecl subTpDecl) {
                HandleExpression(subTpDecl.Constraint);
                if (TargetFound()) // mutate
                    module.SourceDecls.Remove(decl);
                
                if (subTpDecl is NonNullTypeDecl nNullTpDecl) {
                    HandleMemberDecls(nNullTpDecl.Class);
                }
            }

            TargetExpression = null;
        }

        foreach (var sideEffectVar in _sideEffectVarsToDelete) {
            var mutator = new VariableDeletionMutator(
                sideEffectVar.Item1,
                sideEffectVar.Item2,
                reporter
            );
            mutator.Mutate(module);
        }
    }
    
    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members.ToList()) {
            // only visit members that may contain the mutation target
            if (!IsWorthVisiting(member.StartToken.pos, member.EndToken.pos)) continue;
            if (member is Method m) { // includes constructor
                HandleMethod(m);  
            } else if (member is Function func) { // includes predicate
                HandleFunction(func);
            } else if (member is ConstantField cf) {
                if (IsTarget(cf)) // mutate
                    decl.Members.Remove(member);
                if (cf.Rhs == null) continue;
                HandleExpression(cf.Rhs);
                if (TargetFound()) // mutate
                    cf.Rhs = HandleTarget() ?? null;
            }
        }
    }
    
    protected override void HandleFunction(Function function) {
        var functionToken = new AutoGeneratedOrigin(function.Origin);
        Reporter.Info(MessageSource.Rewriter, functionToken, 
            $"This {function.WhatKind} contains the mutation target"
        );
        
        if (function.Body == null) return;
        HandleExpression(function.Body);
        if (TargetFound()) { // mutate
            function.Body = null;
            TargetExpression = null;
        }
    }

    /// ---------------------------
    /// Group of statement visitors
    /// ---------------------------
    protected override void HandleBlock(BlockStmt blockStmt) {
        var prevCurrentScope = _currentScope;
        _currentScope = $"{blockStmt.StartToken.pos}-{blockStmt.EndToken.pos}";
        base.HandleBlock(blockStmt);
        _currentScope = prevCurrentScope;
    }
    
    protected override void HandleBlock(List<Statement> statements) {
        foreach (var stmt in statements.ToList()) {
            HandleStatement(stmt);
            if (!_toDelete.Contains(stmt)) continue; // else mutate
            statements.Remove(stmt);
        }
    }
    
    protected override void VisitStatement(AssignStatement aStmt) {
        foreach (var lhs in aStmt.Lhss.ToList()) {
            var i = aStmt.Lhss.FindIndex(e => e == lhs);
            HandleExpression(lhs);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement == null) {
                    if (aStmt.Lhss.Count == 1 || aStmt.Lhss.Count != aStmt.Rhss.Count) {
                        _toDelete.Add(aStmt);
                        return;
                    }
                    aStmt.Lhss.RemoveAt(i);
                    aStmt.Rhss.RemoveAt(i);
                } else {
                    aStmt.Lhss[i] = replacement;
                }
            }
        }
        foreach (var rhs in aStmt.Rhss.ToList()) {
            var i = aStmt.Rhss.FindIndex(e => e == rhs);
            if (rhs is not ExprRhs exprRhs) continue;
            HandleExpression(exprRhs.Expr);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement == null) {
                    if (aStmt.Rhss.Count == 1 || aStmt.Lhss.Count != aStmt.Rhss.Count) {
                        _toDelete.Add(aStmt);
                        return;
                    }
                    aStmt.Lhss.RemoveAt(i);
                    aStmt.Rhss.RemoveAt(i);
                } else {
                    var oldExpr = (aStmt.Rhss[i] as ExprRhs)?.Expr;
                    var newExprRhs = new ExprRhs(replacement);
                    aStmt.Rhss[i] = newExprRhs;
                    if (aStmt.ResolvedStatements != null && oldExpr != null) 
                        UpdateResolvedStatements(aStmt.ResolvedStatements, replacement, oldExpr);
                }
            }
        }
        
        if (aStmt.OriginalInitialLhs == null) return;
        HandleExpression(aStmt.OriginalInitialLhs);
        if (TargetFound()) // mutate
            aStmt.OriginalInitialLhs = HandleTarget(aStmt) ?? aStmt.OriginalInitialLhs;
    }

    private void UpdateResolvedStatements(List<Statement> resolvedStatements, Expression newExpr, Expression oldExpr) {
        foreach (var stmt in resolvedStatements) {
            if (stmt is not SingleAssignStmt sAStmt) continue;
            if (sAStmt.Rhs is not ExprRhs exprRhs) continue;
            if (exprRhs.Expr != oldExpr) continue;
            exprRhs.Expr = newExpr;
        }
    }
    
    protected override void VisitStatement(AssignSuchThatStmt aStStmt) {
        foreach (var lhs in aStStmt.Lhss.ToList()) {
            var i = aStStmt.Lhss.FindIndex(e => e == lhs);
            HandleExpression(lhs);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget(aStStmt);
                if (replacement == null) return;
                aStStmt.Lhss[i] = replacement;
            }
        }
        if (TargetFound()) return;
        HandleExpression(aStStmt.Expr);
        if (TargetFound()) // mutate
            aStStmt.Expr = HandleTarget(aStStmt) ?? aStStmt.Expr;
    }
    
    protected override void VisitStatement(AssignOrReturnStmt aOrRStmt) {
        foreach (var lhs in aOrRStmt.Lhss.ToList()) {
            var i = aOrRStmt.Lhss.FindIndex(e => e == lhs);
            HandleExpression(lhs);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement == null) {
                    if (aOrRStmt.Lhss.Count == 1 || aOrRStmt.Lhss.Count != aOrRStmt.Rhss.Count) {
                        _toDelete.Add(aOrRStmt);
                        return;
                    }
                    aOrRStmt.Lhss.RemoveAt(i);
                    aOrRStmt.Rhss.RemoveAt(i);
                } else {
                    aOrRStmt.Lhss[i] = replacement;
                }
            }
        }
        foreach (var rhs in aOrRStmt.Rhss.ToList()) {
            var i = aOrRStmt.Rhss.FindIndex(e => e == rhs);
            if (rhs is not ExprRhs exprRhs) continue;
            HandleExpression(exprRhs.Expr);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement == null) {
                    if (aOrRStmt.Rhss.Count == 1 || aOrRStmt.Lhss.Count != aOrRStmt.Rhss.Count) {
                        _toDelete.Add(aOrRStmt);
                        return;
                    }
                    aOrRStmt.Lhss.RemoveAt(i);
                    aOrRStmt.Rhss.RemoveAt(i);
                } else {
                    aOrRStmt.Rhss[i] = new ExprRhs(replacement);
                }
            }
        }
        
        HandleExpression(aOrRStmt.Rhs.Expr);
        if (TargetFound()) // mutate
            aOrRStmt.Rhs.Expr = HandleTarget(aOrRStmt) ?? aOrRStmt.Rhs.Expr ;
    }
    
    protected override void VisitStatement(SingleAssignStmt sAStmt) {
        HandleExpression(sAStmt.Lhs);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(sAStmt);
            if (replacement == null) return;
            sAStmt.Lhs = replacement;
        }
        HandleRhsList([sAStmt.Rhs]);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(sAStmt);
            sAStmt.Rhs = replacement != null ? new ExprRhs(replacement) : sAStmt.Rhs;
        }
    }
    
    protected override void VisitStatement(VarDeclStmt vDeclStmt) {
        foreach (var (e, i) in vDeclStmt.Locals.Select((e, i) => (e, i)).ToList()) {
            if (!IsTarget(e.Name, e.Center.pos)) continue;
            if (vDeclStmt.Locals.Count == 1) {
                _toDelete.Add(vDeclStmt);
                CollectSideEffectVarsToDelete(vDeclStmt);
                return;
            }
            
            vDeclStmt.Locals.RemoveAt(i);
            if (vDeclStmt.Assign == null) continue;
            vDeclStmt.Assign.Lhss.RemoveAt(i);
            switch (vDeclStmt.Assign) {
                case AssignStatement aStmt:
                    if (aStmt.Lhss.Count + 1 != aStmt.Rhss.Count) {
                        _toDelete.Add(vDeclStmt);
                        CollectSideEffectVarsToDelete(vDeclStmt);
                        return;
                    }
                    aStmt.Rhss.RemoveAt(i); 
                    aStmt.ResolvedStatements?.RemoveAt(i); break;
                case AssignSuchThatStmt: 
                    _toDelete.Add(vDeclStmt); 
                    CollectSideEffectVarsToDelete(vDeclStmt);
                    return;
                case AssignOrReturnStmt aOrRStmt: 
                    if (aOrRStmt.Lhss.Count + 1 != aOrRStmt.Rhss.Count) {
                        _toDelete.Add(vDeclStmt);
                        CollectSideEffectVarsToDelete(vDeclStmt);
                        return;
                    }
                    aOrRStmt.Rhss.RemoveAt(i); 
                    aOrRStmt.ResolvedStatements?.RemoveAt(i); break;
            }
        }

        if (vDeclStmt.Assign == null) return;
        HandleStatement(vDeclStmt.Assign);
        if (_toDelete.Contains(vDeclStmt.Assign)) {
            _toDelete.Add(vDeclStmt);
            CollectSideEffectVarsToDelete(vDeclStmt);
        }
    }

    private void CollectSideEffectVarsToDelete(VarDeclStmt vDeclStmt) {
        foreach (var localVar in vDeclStmt.Locals) {
            if (localVar.Name == var) continue;
            _sideEffectVarsToDelete.Add((_currentScope, localVar.Name));
        }
    }
    
    protected override void VisitStatement(VarDeclPattern vDeclPStmt) {
        HandleExpression(vDeclPStmt.RHS);
        if (TargetFound()) // mutate
            vDeclPStmt.RHS = HandleTarget(vDeclPStmt) ?? vDeclPStmt.RHS;
    }
    
    protected override void VisitStatement(ProduceStmt pStmt) {
        if (pStmt.Rhss == null) return;
        HandleRhsList(pStmt.Rhss);
        foreach (var rhs in pStmt.Rhss.ToList()) {
            var i = pStmt.Rhss.FindIndex(e => e == rhs);
            if (rhs is not ExprRhs exprRhs) continue;
            HandleExpression(exprRhs.Expr);
            if (TargetFound()) {
                var replacement = HandleTarget(pStmt);
                if (replacement == null) return;
                pStmt.Rhss[i] = new ExprRhs(replacement);
            }
        }
    }
    
    protected override void VisitStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null) {
            _currentTypeRestriction = new BoolType();
            HandleExpression(ifStmt.Guard);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget(ifStmt);
                if (replacement == null) return;
                ifStmt.Guard = replacement;
            }
            _currentTypeRestriction = null;
        }
        HandleBlock(ifStmt.Thn);
        if (ifStmt.Els == null) return;
        if (ifStmt.Els is BlockStmt bEls) {
            HandleBlock(bEls);
        } else if (ifStmt.Els != null) {
            HandleStatement(ifStmt.Els);
            if (_toDelete.Contains(ifStmt.Els))
                ifStmt.Els = null;
        }
    }
    
    protected override void VisitStatement(WhileStmt whileStmt) {
        _currentTypeRestriction = new BoolType();
        HandleExpression(whileStmt.Guard);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(whileStmt);
            if (replacement == null) return;
            whileStmt.Guard = replacement;
        }
        _currentTypeRestriction = null;
        if (whileStmt.Body != null) HandleBlock(whileStmt.Body);
    }
    
    protected override void VisitStatement(ForLoopStmt forStmt) {
        HandleExpression(forStmt.Start);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(forStmt);
            if (replacement == null) return;
            forStmt.Start = replacement;
        }
        HandleExpression(forStmt.End);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(forStmt);
            if (replacement == null) return;
            forStmt.End = replacement;
        }
        HandleBlock(forStmt.Body);
    }
    
    protected override void VisitStatement(ForallStmt forStmt) {
        HandleExpression(forStmt.Range);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(forStmt);
            if (replacement == null) return;
            forStmt.Range = replacement;
        }
        HandleStatement(forStmt.Body);
        if (_toDelete.Contains(forStmt.Body))
            _toDelete.Add(forStmt);
    }
    
    protected override void VisitStatement(AlternativeLoopStmt altLStmt) {
        foreach (var alt in altLStmt.Alternatives.ToList()) {
            _currentTypeRestriction = new BoolType();
            HandleExpression(alt.Guard);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement != null) {
                    alt.Guard = replacement;
                } else if (altLStmt.Alternatives.Count > 1) {
                    altLStmt.Alternatives.Remove(alt);
                } else {
                    _toDelete.Add(altLStmt);
                }
            }
            _currentTypeRestriction = null;
            HandleBlock(alt.Body);  
        }
    }

    protected override void VisitStatement(AlternativeStmt altStmt) {
        HandleGuardedAlternatives(altStmt.Alternatives);
        foreach (var alt in altStmt.Alternatives.ToList()) {
            HandleExpression(alt.Guard);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement != null) {
                    alt.Guard = replacement;
                } else if (altStmt.Alternatives.Count > 1) {
                    altStmt.Alternatives.Remove(alt);
                } else {
                    _toDelete.Add(altStmt);
                }
            }
            HandleBlock(alt.Body);  
        }
    }
    
    protected override void VisitStatement(CallStmt callStmt) {
        foreach (var lhs in callStmt.Lhs.ToList()) {
            var i = callStmt.Lhs.FindIndex(e => e == lhs);
            HandleExpression(lhs);
            if (!TargetFound()) continue; // else mutate
            var replacement = HandleTarget(callStmt);
            if (replacement == null) return;
            callStmt.Lhs[i] = replacement;
        }
        HandleExpression(callStmt.OriginalInitialLhs);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(callStmt);
            if (replacement == null) return;
            callStmt.OriginalInitialLhs = replacement;
        }
        HandleExpression(callStmt.MethodSelect);
        HandleExpression(callStmt.Receiver);
        HandleMethod(callStmt.Method);
        foreach (var binding in callStmt.Bindings.ArgumentBindings.ToList()) {
            var i = callStmt.Bindings.ArgumentBindings.FindIndex(e => e == binding);
            HandleExpression(binding.Actual);
            if (!TargetFound()) continue; // else mutate
            var replacement = HandleTarget(callStmt);
            if (replacement == null) return;
            callStmt.Bindings.ArgumentBindings[i].Actual = replacement;
        }
        foreach (var arg in callStmt.Args.ToList()) {
            var i = callStmt.Args.FindIndex(e => e == arg);
            HandleExpression(arg);
            if (!TargetFound()) continue; // else mutate
            var replacement = HandleTarget(callStmt);
            if (replacement == null) return;
            callStmt.Args[i] = replacement;
        }
    }
    
    protected override void VisitStatement(ModifyStmt mdStmt) {
        if (mdStmt.Body == null) return;
        HandleStatement(mdStmt.Body);
        if (_toDelete.Contains(mdStmt.Body))
            _toDelete.Add(mdStmt);
    }
    
    protected override void VisitStatement(HideRevealStmt hRStmt) {
        foreach (var expr in hRStmt.Exprs.ToList()) {
            var i = hRStmt.Exprs.FindIndex(e => e == expr);
            HandleExpression(expr);
            if (!TargetFound()) continue;
            var replacement = HandleTarget();
            if (replacement == null) return;
            hRStmt.Exprs[i] = replacement;
        }
    }
    
    protected override void VisitStatement(BlockByProofStmt bBpStmt) {
        HandleStatement(bBpStmt.Body);
        if (_toDelete.Contains(bBpStmt.Body))
            _toDelete.Add(bBpStmt);
    }
    
    protected override void VisitStatement(SkeletonStatement skStmt) {
        if (skStmt.S == null) return;
        HandleStatement(skStmt.S);
        if (_toDelete.Contains(skStmt.S))
            _toDelete.Add(skStmt);
    }
    
    protected override void VisitStatement(PrintStmt prtStmt) {
        foreach (var expr in prtStmt.Args.ToList()) {
            var i = prtStmt.Args.FindIndex(e => e == expr);
            HandleExpression(expr);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget();
            if (replacement != null) {
                prtStmt.Args[i] = replacement;
            } else if (prtStmt.Args.Count > 1) {
                prtStmt.Args.RemoveAt(i);
            } else {
                _toDelete.Add(prtStmt);
                return;
            }
        }
    }
    
    /// ----------------------------
    /// Group of expression visitors
    /// ----------------------------
    protected override void VisitExpression(BinaryExpr bExpr) {
        HandleExpression(bExpr.E0);
        if (TargetFound()) // mutate
            bExpr.E0 = null;
        TargetExpression = null;
        HandleExpression(bExpr.E1);
        if (TargetFound()) // mutate
            bExpr.E1 = null;
        
        if (bExpr.E0 == null || bExpr.E1 == null)
            TargetExpression = bExpr;
    }
    
    protected override void VisitExpression(UnaryExpr uExpr) {
        HandleExpression(uExpr.E);
        if (TargetFound()) // mutate
            uExpr.E = HandleTarget(uExpr) ?? uExpr.E;
    }
    
    protected override void VisitExpression(ParensExpression pExpr) {
        HandleExpression(pExpr.E);
        if (TargetFound()) // mutate
            pExpr.E = HandleTarget(pExpr) ?? pExpr.E;
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        HandleExpression(nExpr.E);
        if (TargetFound()) // mutate
            nExpr.E = HandleTarget(nExpr) ?? nExpr.E;
    }

    protected override void VisitExpression(ChainingExpression cExpr) {
        foreach (var (e, i) in cExpr.Operands.Select((e, i) => (e, i)).ToList()) {
            HandleExpression(e);
            if (!TargetFound()) continue;
            TargetExpression = cExpr;
            return;
        }
    }
    
    protected override void VisitExpression(LetExpr ltExpr) {
        HandleExpression(ltExpr.Body);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(ltExpr);
            if (replacement == null) return;
            ltExpr.Body = replacement;
        }
        foreach (var rhs in ltExpr.RHSs.ToList()) {
            var i = ltExpr.RHSs.FindIndex(e => e == rhs);
            HandleExpression(rhs);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget();
            if (replacement != null) {
                ltExpr.RHSs[i] = replacement;
            } else if (ltExpr.RHSs.Count > 1) {
                ltExpr.RHSs.RemoveAt(i);
            } else {
                TargetExpression = ltExpr;
            }
        }
    }
    
    protected override void VisitExpression(LetOrFailExpr ltOrFExpr) {
        HandleExpression(ltOrFExpr.Rhs);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(ltOrFExpr);
            if (replacement == null) return;
            ltOrFExpr.Rhs = replacement;
        }
        HandleExpression(ltOrFExpr.Body);
        if (TargetFound()) // mutate
            ltOrFExpr.Body = HandleTarget(ltOrFExpr) ?? ltOrFExpr.Body;
    }
    
    protected override void VisitExpression(ApplyExpr appExpr) {
        foreach (var arg in appExpr.Args.ToList()) {
            var i = appExpr.Args.FindIndex(e => e == arg);
            HandleExpression(arg);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget(appExpr);
            if (replacement == null) return;
            appExpr.Args[i] = replacement;
        }
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        HandleExpression(suffixExpr.Lhs);
        if (TargetFound()) { // mutate
            suffixExpr.Lhs = HandleTarget(suffixExpr) ?? suffixExpr.Lhs;
        }

        if (suffixExpr is ExprDotName exprDName && 
            IsTarget(exprDName.SuffixNameNode.Value, exprDName.SuffixNameNode.Center.pos)) {
            TargetExpression = suffixExpr;
            return;
        }

        if (suffixExpr is not ApplySuffix appSufExpr) return;
        foreach (var binding in appSufExpr.Bindings.ArgumentBindings.ToList()) {
            var i = appSufExpr.Bindings.ArgumentBindings.FindIndex(e => e == binding);
            HandleExpression(binding.Actual);
            if (!TargetFound()) continue;
            var replacement = HandleTarget(suffixExpr);
            if (replacement == null) return;
            appSufExpr.Bindings.ArgumentBindings[i].Actual = replacement;
        }
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        HandleExpression(fCallExpr.Receiver);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(fCallExpr);
            if (replacement == null) return;
            fCallExpr.Receiver = replacement;
        }
        foreach (var binding in fCallExpr.Bindings.ArgumentBindings.ToList()) {
            var i = fCallExpr.Bindings.ArgumentBindings.FindIndex(e => e == binding);
            HandleExpression(binding.Actual);
            if (!TargetFound()) continue;
            var replacement = HandleTarget(fCallExpr);
            if (replacement == null) return;
            fCallExpr.Bindings.ArgumentBindings[i].Actual = replacement;
        }
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        HandleExpression(mSelExpr.Obj);
        if (TargetFound()) // mutate
            mSelExpr.Obj = HandleTarget(mSelExpr) ?? mSelExpr.Obj;
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {      
        HandleExpression(iteExpr.Test);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(iteExpr);
            if (replacement == null) return;
            iteExpr.Test = replacement;
        }
        HandleExpression(iteExpr.Thn);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(iteExpr);
            if (replacement == null) return;
            iteExpr.Thn = replacement;
        }
        HandleExpression(iteExpr.Els);
        if (TargetFound()) // mutate
            iteExpr.Els = HandleTarget(iteExpr) ?? iteExpr.Els;
    }
    
    protected override void VisitExpression(MatchExpr mExpr) {
        foreach (var c in mExpr.Cases) {
            HandleExpression(c.Body);
            if (!TargetFound()) continue; // mutate
            TargetExpression = mExpr;
            return;
        }
        
        HandleExpression(mExpr.Source);
        if (TargetFound()) // mutate
            TargetExpression = mExpr;
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        foreach (var c in nMExpr.Cases.ToList()) {
            HandleExpression(c.Body);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget();
            if (replacement == null) return;
            c.Body = replacement;
        }
        
        HandleExpression(nMExpr.Source);
        if (TargetFound()) // mutate
            TargetExpression = nMExpr;
    }
    
    protected override void VisitExpression(DisplayExpression dExpr) {
        HandleExprList(dExpr.Elements);
        foreach (var element in dExpr.Elements.ToList()) {
            var i = dExpr.Elements.FindIndex(e => e == element);
            HandleExpression(element);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget();
            if (replacement != null) {
                dExpr.Elements[i] = replacement;
            } else {
                dExpr.Elements.RemoveAt(i);
            }
        }
    }
    
    protected override void VisitExpression(MapDisplayExpr mDExpr) {
        foreach (var elem in mDExpr.Elements.ToList()) {
            HandleExpression(elem.A);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement != null) {
                    elem.A = replacement;
                } else {
                    mDExpr.Elements.Remove(elem);
                    continue;
                }
            }
            HandleExpression(elem.B);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget();
                if (replacement != null) {
                    elem.B = replacement;
                } else {
                    mDExpr.Elements.Remove(elem);
                }
            }
        }
    }
    
    protected override void VisitExpression(SeqConstructionExpr seqCExpr) {
        HandleExpression(seqCExpr.N);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(seqCExpr);
            if (replacement == null) return;
            seqCExpr.N = replacement;
        }
        HandleExpression(seqCExpr.Initializer);
        if (TargetFound()) // mutate
            seqCExpr.Initializer = HandleTarget(seqCExpr) ?? seqCExpr.Initializer;
    }
    
    protected override void VisitExpression(MultiSetFormingExpr mSetFExpr) { 
        HandleExpression(mSetFExpr.E);
        if (TargetFound()) // mutate
            mSetFExpr.E = HandleTarget(mSetFExpr) ?? mSetFExpr.E;
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        HandleExpression(seqSExpr.Seq);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(seqSExpr);
            if (replacement == null) return;
            seqSExpr.Seq = replacement;
        }
        if (seqSExpr.E0 != null) {
            HandleExpression(seqSExpr.E0);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget(seqSExpr);
                if (replacement == null) return;
                seqSExpr.E0 = replacement;
            }
        }
        if (seqSExpr.E1 == null) return;
        HandleExpression(seqSExpr.E1);
        if (TargetFound()) // mutate
            seqSExpr.E1 = HandleTarget(seqSExpr) ?? seqSExpr.E1;
    }
    
    protected override void VisitExpression(MultiSelectExpr mSExpr) {
        HandleExpression(mSExpr.Array);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(mSExpr);
            if (replacement == null) return;
            mSExpr.Array = replacement;
        }
        foreach (var index in mSExpr.Indices.ToList()) {
            var i = mSExpr.Indices.FindIndex(e => e == index);
            HandleExpression(index);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget();
            if (replacement != null) {
                mSExpr.Indices[i] = replacement;
            } else if (mSExpr.Indices.Count > 1) {
                mSExpr.Indices.RemoveAt(i);
            } else {
                TargetExpression = mSExpr;
            }
        }
    }
    
    protected override void VisitExpression(SeqUpdateExpr seqUExpr) {
        HandleExpression(seqUExpr.Seq);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(seqUExpr);
            if (replacement == null) return;
            seqUExpr.Seq = replacement;
        }
        HandleExpression(seqUExpr.Index);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(seqUExpr);
            if (replacement == null) return;
            seqUExpr.Index = replacement;
        }
        HandleExpression(seqUExpr.Value);
        if (TargetFound()) // mutate
            seqUExpr.Value = HandleTarget(seqUExpr) ?? seqUExpr.Value;
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        HandleExpression(compExpr.Term);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(compExpr);
            if (replacement == null) return; 
            compExpr.Term = replacement;
        }
        if (compExpr.Range != null) {
            HandleExpression(compExpr.Range);
            if (TargetFound()) { // mutate
                var replacement = HandleTarget(compExpr);
                if (replacement == null) return;
                compExpr.Range = replacement;
            }
        }

        if (compExpr is not MapComprehension mCompExpr || mCompExpr.TermLeft == null) return;
        HandleExpression(mCompExpr.TermLeft);
        if (TargetFound()) // mutate
            mCompExpr.TermLeft = HandleTarget(compExpr) ?? mCompExpr.TermLeft;
    }
    
    protected override void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        HandleExpression(dtUExpr.Root);
        if (TargetFound()) { // mutate
            var replacement = HandleTarget(dtUExpr);
            if (replacement == null) return;
            dtUExpr.Root = replacement;
        }

        foreach (var update in dtUExpr.Updates.ToList()) {
            var i = dtUExpr.Updates.FindIndex(e => Equals(e, update));
            HandleExpression(update.Item3);
            if (!TargetFound()) continue; // mutate
            var replacement = HandleTarget();
            if (replacement != null) {
                var newUpdate = Tuple.Create(update.Item1, update.Item2, replacement);
                dtUExpr.Updates[i] = newUpdate;
            } else if (dtUExpr.Updates.Count > 1) {
                dtUExpr.Updates.RemoveAt(i);
            } else {
                TargetExpression = dtUExpr;
            }
        }
    }
    
    protected override void VisitExpression(DatatypeValue dtValue) {
        foreach (var binding in dtValue.Bindings.ArgumentBindings.ToList()) {
            var i = dtValue.Bindings.ArgumentBindings.FindIndex(e => e == binding);
            HandleExpression(binding.Actual);
            if (!TargetFound()) continue;
            var replacement = HandleTarget(dtValue);
            if (replacement != null) {
                dtValue.Bindings.ArgumentBindings[i].Actual = replacement;
            } else {
                dtValue.Bindings.ArgumentBindings.RemoveAt(i);
            }
        }
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        HandleStatement(stmtExpr.S);
        if (_toDelete.Contains(stmtExpr.S)) {
            TargetExpression = stmtExpr;
            return;
        }
        HandleExpression(stmtExpr.E);
        if (TargetFound()) // mutate
            stmtExpr.E = HandleTarget(stmtExpr) ?? stmtExpr.E;
    }
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    protected override bool IsWorthVisiting(int tokenStartPos, int tokenEndPos) {
        if (MutationTargetPos == "-") return true;
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        return (tokenStartPos <= int.Parse(positions[0]) &&
                int.Parse(positions[1]) <= tokenEndPos);
    }
}