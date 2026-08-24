using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class ExprCollectionLengthReplacement(string mutationTargetPos, string arg, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    private ChainingExpression? _chainingExpressionParent;
    
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        var argElems = arg.Split(':');
        if (argElems.Length != 2) return originalExpr;
        var collection = argElems[0];
        var collectionNameSeg = new NameSegment(originalExpr.Origin, collection, null);
        var collectionType = argElems[1];

        Expression mutatedExpr = collectionType.StartsWith("array<") ? 
            new ExprDotName(null, collectionNameSeg, new Name("Length"), null) : 
            new UnaryOpExpr(null, UnaryOpExpr.Opcode.Cardinality, collectionNameSeg);
        
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
        MutantGenerator.NumMutations++;
        MutantGenerator.MutatedNodes.Add(mutatedExpr);
        ForbidChildrenMutation(mutatedExpr);
        return mutatedExpr;
    }
    
    private bool IsTarget(Expression expr) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        return expr.StartToken.pos == startPosition && 
               expr.EndToken.pos == endPosition && 
               !AlreadyMutated(expr) &&
               !ContainsMutatedChildren(expr);
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
    
    protected override void VisitExpression(ChainingExpression cExpr) {
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
    
    protected override void VisitExpression(MatchExpr mExpr) {
        if (IsTarget(mExpr)) {
            TargetExpression = mExpr;
            return;
        }
        base.VisitExpression(mExpr);
    }
    
    protected override void VisitExpression(NestedMatchExpr nMExpr) {
        if (IsTarget(nMExpr)) {
            TargetExpression = nMExpr;
            return;
        }
        base.VisitExpression(nMExpr);
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
    
    /// ----------------------
    /// Group of visitor utils
    /// ----------------------
    protected override void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
            if (!TargetFound()) return; // else mutate
            exprRhs.Expr = CreateMutatedExpression(exprRhs.Expr);
        } else if (aRhs is TypeRhs tpRhs) {
            var elInit = tpRhs.ElementInit;
            
            if (tpRhs.ArrayDimensions != null) {
                foreach (var (dim, i) in tpRhs.ArrayDimensions.Select((dim, i) => (dim, i))) {
                    HandleExpression(dim);
                    if (!TargetFound()) continue;
                    tpRhs.ArrayDimensions[i] = CreateMutatedExpression(dim);
                    break;
                }
            } if (elInit != null && IsWorthVisiting(elInit.StartToken.pos, elInit.EndToken.pos)) {
                HandleExpression(elInit);
            } if (tpRhs.InitDisplay != null) {
                foreach (var (init, i) in tpRhs.InitDisplay.Select((init, i) => (init, i))) {
                    HandleExpression(init);
                    if (!TargetFound()) continue;
                    tpRhs.InitDisplay[i] = CreateMutatedExpression(init);
                    break;
                }
            } if (tpRhs.Bindings != null) {
                HandleActualBindings(tpRhs.Bindings);
            }
        }
    }
}