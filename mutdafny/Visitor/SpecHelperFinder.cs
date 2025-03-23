using Microsoft.Dafny;
using Expression = Microsoft.Dafny.Expression;

namespace MutDafny.Visitor;

// this type of finder is used to get a list of variables, functions, predicates, etc. that are used as a part of the specification
public class SpecHelperFinder(ErrorReporter reporter): Visitor(-1, reporter)
{
    public static List<string> SpecHelpers { get; } = [];
    private bool _isInsideSpec;
    
    /// ---------------------------
    /// Group of top level visitors
    /// ---------------------------
    public override void Find(ModuleDefinition module) {
        HandleDefaultClassDecl(module);
        HandleSourceDecls(module);
    }

    protected override void  HandleSourceDecls(ModuleDefinition module) {
        foreach (var decl in module.SourceDecls) {
            if (decl is TopLevelDeclWithMembers declWithMembers) { // includes class, trait, datatype, etc.
                HandleMemberDecls(declWithMembers);
            }
            if (decl is IteratorDecl itDecl) {
                VisitReqEns(itDecl.Requires);
                VisitReqEns(itDecl.Ensures);
                VisitReqEns(itDecl.YieldRequires);
                VisitReqEns(itDecl.YieldEnsures);
                VisitDecreases(itDecl.Decreases);
                VisitReadsModifies(itDecl.Reads);
                VisitReadsModifies(itDecl.Modifies);
                HandleBlock(itDecl.Body);
            } else if (decl is NewtypeDecl newTpDecl) {
                HandleExpression(newTpDecl.Constraint);
            } else if (decl is SubsetTypeDecl subTpDecl) {
                HandleExpression(subTpDecl.Constraint);
                if (subTpDecl is NonNullTypeDecl nNullTpDecl) {
                    HandleMemberDecls(nNullTpDecl.Class);
                }
            } else if (decl is ArrowTypeDecl aTpDecl) {
                VisitReqEns(aTpDecl.Requires.Req);
                VisitReqEns(aTpDecl.Requires.Ens);
                VisitReqEns(aTpDecl.Reads.Req);
                VisitReqEns(aTpDecl.Reads.Ens);
            }
        }
    }

    protected override void HandleMemberDecls(TopLevelDeclWithMembers decl) {
        foreach (var member in decl.Members) {
            if (member is MethodOrFunction mf) {
                VisitReqEns(mf.Req);
                VisitReqEns(mf.Ens);
                VisitDecreases(mf.Decreases);
                VisitReadsModifies(mf.Reads);
            }
            if (member is Method m) { // includes lemma
                VisitReadsModifies(m.Mod);   
                if (m.Body == null) return;
                HandleBlock(m.Body);
            } else if (member is Function f) { // includes predicate
                if (f.Body == null) return;
                HandleExpression(f.Body);
            }
        }
    }

    private void VisitReqEns(List<AttributedExpression> attExprs) {
        _isInsideSpec = true;
        var exprs = attExprs.Select(e => e.E).ToList();
        HandleExprList(exprs);
        _isInsideSpec = false;
    }

    private void VisitDecreases(Specification<Expression> expr) {
        _isInsideSpec = true;
        if (expr.Expressions == null) return;
        HandleExprList(expr.Expressions);
        _isInsideSpec = false;
    }

    private void VisitReadsModifies(Specification<FrameExpression> expr) {
        _isInsideSpec = true;
        if (expr.Expressions == null) return;
        var exprs = expr.Expressions.Select(e => e.E).ToList();
        HandleExprList(exprs);
        _isInsideSpec = false;
    }

    /// --------------------------------------------
    /// Group of overriden statement visitors
    /// Statements that are/contain part of the spec
    /// --------------------------------------------
    private void VisitStatement(LoopStmt loopStmt) {
        VisitReqEns(loopStmt.Invariants);
        VisitDecreases(loopStmt.Decreases);
        VisitReadsModifies(loopStmt.Mod);
    }
    
    protected override void VisitStatement(WhileStmt whileStmt) {
        VisitStatement(whileStmt);
        base.VisitStatement(whileStmt);
    }

    protected override void VisitStatement(ForLoopStmt forStmt) {
        VisitStatement(forStmt);
        base.VisitStatement(forStmt);
    }
    
    protected override void VisitStatement(AlternativeLoopStmt altLStmt) {
        VisitStatement(altLStmt);
        base.VisitStatement(altLStmt);
    }
    
    protected override void VisitStatement(ForallStmt forStmt) {
        VisitReqEns(forStmt.Ens);
        base.VisitStatement(forStmt);
    }
    
    protected override void VisitStatement(ModifyStmt mdStmt) {
        VisitReadsModifies(mdStmt.Mod);
        base.VisitStatement(mdStmt);
    }
    protected override void VisitStatement(BlockByProofStmt bBpStmt) {
        _isInsideSpec = true;
        HandleStatement(bBpStmt.Proof);
        _isInsideSpec = false;
        base.VisitStatement(bBpStmt);
    }
    protected override void VisitStatement(OpaqueBlock opqBlock) {
        VisitReqEns(opqBlock.Ensures);
        VisitReadsModifies(opqBlock.Modifies);
    }
    
    // includes AssertStmt, AssumeStmt, ExpectStmt
    protected override void VisitStatement(PredicateStmt predStmt) {
        _isInsideSpec = true;
        HandleExpression(predStmt.Expr);
        _isInsideSpec = false;
    }

    protected override void VisitStatement(CalcStmt calcStmt) {
        _isInsideSpec = true;
        HandleExprList(calcStmt.Lines);
        foreach (var stmt in calcStmt.Hints) {
            HandleStatement(stmt);
        }
        _isInsideSpec = false;
    }
    
    /// ---------------------------------------------
    /// Group of overriden expression visitors
    /// Expressions that are/contain part of the spec
    /// ---------------------------------------------
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        if (compExpr is LambdaExpr lExpr) {
            VisitReadsModifies(lExpr.Reads);
        }
        base.VisitExpression(compExpr);
    }

    protected override void VisitExpression(OldExpr oldExpr) {
        _isInsideSpec = true;
        HandleExpression(oldExpr.E);
        _isInsideSpec = false;
    }

    protected override void VisitExpression(UnchangedExpr unchExpr) {
        _isInsideSpec = true;
        var exprs = unchExpr.Frame.Select(e => e.E).ToList();
        HandleExprList(exprs);
        _isInsideSpec = false;
    }

    protected override void VisitExpression(DecreasesToExpr dToExpr) {
        _isInsideSpec = true;
        HandleExprList(dToExpr.OldExpressions.ToList());
        HandleExprList(dToExpr.NewExpressions.ToList());
        _isInsideSpec = false;
    }

    /// --------------------------------------------------
    /// Expressions that identify function/predicate calls
    /// --------------------------------------------------
    protected override void VisitExpression(ApplyExpr appExpr) {
        if (_isInsideSpec) {
            if (appExpr.Function is NameSegment ns) {
                SpecHelpers.Add(ns.Name);
            } else if (appExpr.Function is IdentifierExpr id) {
                SpecHelpers.Add(id.Name);
            }
        }
        base.VisitExpression(appExpr);
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (_isInsideSpec) {
            if (suffixExpr is ApplySuffix appSufExpr) {
                VisitExpression(appSufExpr);
            } else if (suffixExpr is ExprDotName exprDotN) {
                VisitExpression(exprDotN);
            }
        }
        base.VisitExpression(suffixExpr);
    }

    private void VisitExpression(ApplySuffix appSufExpr) {
        if (appSufExpr.Lhs is NameSegment ns) {
            SpecHelpers.Add(ns.Name);
        } else if (appSufExpr.Lhs is IdentifierExpr id) {
            SpecHelpers.Add(id.Name);
        }
    }
    
    private void VisitExpression(ExprDotName exprDotN) {
        SpecHelpers.Add(exprDotN.SuffixName);
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        if (_isInsideSpec) SpecHelpers.Add(fCallExpr.Name);
        base.VisitExpression(fCallExpr);
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        if (_isInsideSpec) SpecHelpers.Add(mSelExpr.MemberName);
        base.VisitExpression(mSelExpr);
    }
}