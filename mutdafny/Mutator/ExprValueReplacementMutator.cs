using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

// this mutation operator replaces an expression with a literal value of the same type
public class ExprValueReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;

    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        Expression mutatedExpr = val switch {
            "int" => new LiteralExpr(originalExpr.Origin, 0),
            "real" => new LiteralExpr(originalExpr.Origin, BigDec.ZERO),
            "bv" => new LiteralExpr(originalExpr.Origin, BigInteger.Zero),
            "char" => new CharLiteralExpr(originalExpr.Origin, "0"),
            "string" => new StringLiteralExpr(originalExpr.Origin, "", false),
            "set" => new SetDisplayExpr(originalExpr.Origin, true, []),
            "multiset" => new MultiSetDisplayExpr(originalExpr.Origin, []),
            "seq" => new SeqDisplayExpr(originalExpr.Origin, []),
            "map" => new MapDisplayExpr(originalExpr.Origin, true, []),
            _ => new LiteralExpr(originalExpr.Origin, null)
        };
        
        if (_chainingExpressionParent != null) {
            var operands = _chainingExpressionParent.Operands;
            foreach (var (e, i) in operands.Select((e, i) => (e, i)).ToList()) {
                if (e != TargetExpression) continue;
                operands[i] = mutatedExpr;
            }
            mutatedExpr = new ChainingExpression(_chainingExpressionParent.Origin, operands, 
                _chainingExpressionParent.Operators, _chainingExpressionParent.OperatorLocs, 
                _chainingExpressionParent.PrefixLimits);
        }

        TargetExpression = null;
        return mutatedExpr;
    }
    
    private TypeRhs CreateArrayInit(AssignmentRhs originalRhs) {
        return new TypeRhs(originalRhs.Origin, 
            new IntType(originalRhs.Origin), 
            new LiteralExpr(originalRhs.Origin, 0), 
            []
        );
    }

    private ExprRhs CreateNullExprRhs(AssignmentRhs aRhs) {
        var nullExpr = new LiteralExpr(aRhs.Origin, null);
        return new ExprRhs(aRhs.Origin, nullExpr);
    }
    
    private bool IsTarget(Expression expr) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return expr.StartToken.pos == startPosition && expr.EndToken.pos == endPosition;
    }

    private bool IsTarget(TypeRhs typeRhs) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return typeRhs.StartToken.pos == startPosition && typeRhs.EndToken.pos == endPosition;
    }
    
    /// ----------------------------
    /// Group of expression visitors
    /// ----------------------------
    protected override void VisitExpression(LiteralExpr litExpr) {
        if (IsTarget(litExpr)) {
            TargetExpression = litExpr;
        }
    }
    
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (IsTarget(bExpr)) {
            TargetExpression = bExpr;
            return;
        }
        base.VisitExpression(bExpr);
    }
    
    protected override void VisitExpression(UnaryExpr uExpr) {
        if (IsTarget(uExpr)) {
            TargetExpression = uExpr;
            return;
        }
        base.VisitExpression(uExpr);
    }
    
    protected override void VisitExpression(ParensExpression pExpr) {
        if (IsTarget(pExpr)) {
            TargetExpression = pExpr;
            return;
        }
        base.VisitExpression(pExpr);
    }
    
    protected override void VisitExpression(NegationExpression nExpr) {
        if (IsTarget(nExpr)) {
            TargetExpression = nExpr;
            return;
        }
        base.VisitExpression(nExpr);
    }
    
    protected override void VisitExpression(ChainingExpression cExpr) {
        if (IsTarget(cExpr)) {
            TargetExpression = cExpr;
            return;
        }
        
        foreach (var operand in cExpr.Operands) {
            if (IsTarget(operand)) {
                TargetExpression = operand;
                _chainingExpressionParent = cExpr;
                return;
            }
        }
    }

    protected override void VisitExpression(NameSegment nSegExpr) {
        if (IsTarget(nSegExpr)) {
            TargetExpression = nSegExpr;
        }
    }
    
    protected override void VisitExpression(LetExpr ltExpr) {
        if (IsTarget(ltExpr)) {
            TargetExpression = ltExpr;
            return;
        }
        base.VisitExpression(ltExpr);
    }
    
    protected override void VisitExpression(LetOrFailExpr ltOrFExpr) {
        if (IsTarget(ltOrFExpr)) {
            TargetExpression = ltOrFExpr;
            return;
        }
        base.VisitExpression(ltOrFExpr);
    }
    
    protected override void VisitExpression(ApplyExpr appExpr) {
        if (IsTarget(appExpr)) {
            TargetExpression = appExpr;
            return;
        }
        base.VisitExpression(appExpr);
    }
    
    protected override void VisitExpression(SuffixExpr suffixExpr) {
        if (IsTarget(suffixExpr)) {
            TargetExpression = suffixExpr;
            return;
        }
        base.VisitExpression(suffixExpr);
    }
    
    protected override void VisitExpression(FunctionCallExpr fCallExpr) {
        if (IsTarget(fCallExpr)) {
            TargetExpression = fCallExpr;
            return;
        }
        base.VisitExpression(fCallExpr);
    }
    
    protected override void VisitExpression(MemberSelectExpr mSelExpr) {
        if (IsTarget(mSelExpr)) {
            TargetExpression = mSelExpr;
            return;
        }
        base.VisitExpression(mSelExpr);
    }
    
    protected override void VisitExpression(ITEExpr iteExpr) {
        if (IsTarget(iteExpr)) {
            TargetExpression = iteExpr;
            return;
        }
        base.VisitExpression(iteExpr);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        if (IsTarget(nMExpr)) {
            TargetExpression = nMExpr;
            return;
        }
        base.VisitExpression(nMExpr);
    }
    
    protected override void VisitExpression(SeqConstructionExpr seqCExpr) {
        if (IsTarget(seqCExpr)) {
            TargetExpression = seqCExpr;
            return;
        }
        base.VisitExpression(seqCExpr);
    }
    
    protected override void VisitExpression(MultiSetFormingExpr mSetFExpr) {
        if (IsTarget(mSetFExpr)) {
            TargetExpression = mSetFExpr;
            return;
        }
        base.VisitExpression(mSetFExpr);
    }
    
    protected override void VisitExpression(SeqSelectExpr seqSExpr) {
        if (IsTarget(seqSExpr)) {
            TargetExpression = seqSExpr;
            return;
        }
        base.VisitExpression(seqSExpr);
    }
    
    protected override void VisitExpression(MultiSelectExpr mSExpr) {
        if (IsTarget(mSExpr)) {
            TargetExpression = mSExpr;
            return;
        }
        base.VisitExpression(mSExpr);
    }
    
    protected override void VisitExpression(SeqUpdateExpr seqUExpr) {
        if (IsTarget(seqUExpr)) {
            TargetExpression = seqUExpr;
            return;
        }
        base.VisitExpression(seqUExpr);
    }
    
    protected override void VisitExpression(ComprehensionExpr compExpr) {
        if (IsTarget(compExpr)) {
            TargetExpression = compExpr;
            return;
        }
        base.VisitExpression(compExpr);
    }
    
    protected override void VisitExpression(DatatypeUpdateExpr dtUExpr) {
        if (IsTarget(dtUExpr)) {
            TargetExpression = dtUExpr;
            return;
        }
        base.VisitExpression(dtUExpr);
    }
    
    protected override void VisitExpression(StmtExpr stmtExpr) {
        if (IsTarget(stmtExpr)) {
            TargetExpression = stmtExpr;
            return;
        }
        base.VisitExpression(stmtExpr);
    }
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    protected override void HandleRhsList(List<AssignmentRhs> rhss) {
        foreach (var (rhs, i) in rhss.Select((rhs, i) => (rhs, i)).ToList()) {
            if (!IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos))
                continue;
            HandleAssignmentRhs(rhs);
            if (!TargetFound()) continue;
            if (TargetExpression != null) {
                TargetExpression = null;
                rhss[i] = CreateArrayInit(rhs);
            } else if (TargetAssignmentRhs != null) {
                TargetAssignmentRhs = null;
                rhss[i] = CreateNullExprRhs(rhs);
            }
        }
    }
    
    protected override void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
            if (!TargetFound()) return; // else mutate
            if (val != "array") { // array mutations need to be done at higher level: replace ExprRhs with TypeRhs
                exprRhs.Expr = CreateMutatedExpression(exprRhs.Expr);
            }
        } else if (aRhs is TypeRhs tpRhs) {
            if (IsTarget(tpRhs)) {
                TargetAssignmentRhs = tpRhs;
                return;
            }
            
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
}