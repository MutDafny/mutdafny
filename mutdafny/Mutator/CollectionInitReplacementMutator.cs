using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

// this mutation operator replaces a collection's empty initialization with a default non-empty one
// and non-empty initialization with an empty one
public class CollectionInitReplacementMutator(string mutationTargetPos, string val, ErrorReporter reporter) 
    : ExprReplacementMutator(mutationTargetPos, reporter)
{
    protected override Expression CreateMutatedExpression(Expression originalExpr) {
        TargetExpression = null;
        List<Expression> elements = []; 
        List<ExpressionPair> pairElements = [];
        if (originalExpr is MapDisplayExpr) {
            pairElements = CreatePairElements(originalExpr, val);
        } else {
            elements = CreateElements(originalExpr, val);
        }

        return originalExpr switch {
            SetDisplayExpr => new SetDisplayExpr(originalExpr.Origin, true, elements),
            MultiSetDisplayExpr => new MultiSetDisplayExpr(originalExpr.Origin, elements),
            SeqDisplayExpr => new SeqDisplayExpr(originalExpr.Origin, elements),
            MapDisplayExpr => new MapDisplayExpr(originalExpr.Origin, true, pairElements),
            _ => new LiteralExpr(originalExpr.Origin, null)
        };
    }

    private TypeRhs CreateArrayInit(TypeRhs originalRhs) {
        return new TypeRhs(originalRhs.Origin,
            CreateType(originalRhs, val),
            new LiteralExpr(originalRhs.Origin, 3),
            CreateElements(originalRhs, val)
        );
    }

    private List<Expression> CreateElements(INode originalNode, string type) {
        return type switch {
            "int" => [
                new LiteralExpr(originalNode.Origin, 1),
                new LiteralExpr(originalNode.Origin, 2),
                new LiteralExpr(originalNode.Origin, 3)
            ],
            "real" => [
                new LiteralExpr(originalNode.Origin, BigDec.FromString("1.0")),
                new LiteralExpr(originalNode.Origin, BigDec.FromString("2.0")),
                new LiteralExpr(originalNode.Origin, BigDec.FromString("3.0"))
            ],
            "bv" => [
                new LiteralExpr(originalNode.Origin, new BigInteger(1)),
                new LiteralExpr(originalNode.Origin, new BigInteger(2)),
                new LiteralExpr(originalNode.Origin, new BigInteger(3))
            ],
            "bool" => [
                new LiteralExpr(originalNode.Origin, true), 
                new LiteralExpr(originalNode.Origin, false),
                new LiteralExpr(originalNode.Origin, true)
            ],
            "char" => [
                new CharLiteralExpr(originalNode.Origin, "a"),
                new CharLiteralExpr(originalNode.Origin, "b"),
                new CharLiteralExpr(originalNode.Origin, "c")
            ],
            "string" => [
                new StringLiteralExpr(originalNode.Origin, "Hello", false),
                new StringLiteralExpr(originalNode.Origin, "World", false),
                new StringLiteralExpr(originalNode.Origin, "!", false),
            ],
            _ => []
        };
    }

    private List<ExpressionPair> CreatePairElements(Expression originalExpr, string type) {
        if (type == "")
            return [];
        
        var types = type.Split("-");
        var keyType = types[0];
        var valueType = types[1];
        var keyElements = CreateElements(originalExpr, keyType);
        var valueElements = CreateElements(originalExpr, valueType);
        List<ExpressionPair> pairElements = [];

        for (var i = 0; i < keyElements.Count; i++) {
            pairElements.Add(new ExpressionPair(keyElements[i], valueElements[i]));
        }
        return pairElements;
    }

    private Type CreateType(TypeRhs originalRhs, string type) {
        return type switch {
            "int" => new IntType(originalRhs.Origin),
            "real" => new RealType(),
            "bv" => new BitvectorType(new DafnyOptions(null, null, null), 4),
            "bool" => new BoolType(),
            "char" => new CharType(),
            _ => new UserDefinedType(originalRhs.Origin, "string", [])
        };
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
    
    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void VisitExpression(DisplayExpression dExpr) {
        if (IsTarget(dExpr)) {
            TargetExpression = dExpr;
            return;
        }
        base.VisitExpression(dExpr);
    }
    
    protected override void VisitExpression(MapDisplayExpr mDExpr) {
        if (IsTarget(mDExpr)) {
            TargetExpression = mDExpr;
            return;
        }
        base.VisitExpression(mDExpr);
    }
    
    protected override void HandleRhsList(List<AssignmentRhs> rhss) {
        foreach (var (rhs, i) in rhss.Select((rhs, i) => (rhs, i)).ToList()) {
            if (!IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos))
                continue;
            HandleAssignmentRhs(rhs);
            if (TargetFound() && rhs is TypeRhs tpRhs && val != "") {
                TargetAssignmentRhs = null;
                rhss[i] = CreateArrayInit(tpRhs);
            }
        }

        foreach (var (rhs, i) in rhss.Select((rhs, i) => (rhs, i))) {
            if (!IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos))
                continue;
            HandleAssignmentRhs(rhs);
            if (TargetFound() && rhs is TypeRhs tpRhs && val == "") {
                TargetAssignmentRhs = null;
                tpRhs.ArrayDimensions = [new LiteralExpr(tpRhs.Origin, 0)];
                tpRhs.InitDisplay = null;
            }
        }
    }
    
    protected override void HandleAssignmentRhs(AssignmentRhs aRhs) {
        if (aRhs is ExprRhs exprRhs) {
            HandleExpression(exprRhs.Expr);
            if (TargetFound()) // mutate
                exprRhs.Expr = CreateMutatedExpression(exprRhs.Expr);
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