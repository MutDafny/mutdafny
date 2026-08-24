using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;
using Type = Microsoft.Dafny.Type;

namespace MutDafny.Mutator;

public class CollectionUpdateStmtMutator(string insertPos, string collectionTypeStr, string collectionUpdateType, ErrorReporter reporter) 
    : Mutator(insertPos, reporter)
{
    private int _assignLhsIndex = -1;
    private int _methodOutsIndex = -1;
    private BlockStmt? _currentBlock;
    private IOrigin? _mutOrigin;
    
    private Statement? Mutate(AssignStatement aStmt) {
        if (_currentBlock == null) return null;
        
        NameSegment? collection = null;
        if (_assignLhsIndex != -1 && aStmt.Lhss.Count > _assignLhsIndex) {
            if (aStmt.Lhss[_assignLhsIndex] is NameSegment nSegExpr) {
                collection = nSegExpr;
            } else if (aStmt.Lhss[_assignLhsIndex] is SeqSelectExpr { Seq: NameSegment seqNSegExpr }) {
                collection = seqNSegExpr;
            }
        }
        if (collection == null) return null;

        _mutOrigin = aStmt.EndToken;
        var collectionUpdateStmt = CreateCollectionUpdateStmt(collection);
        if (collectionUpdateStmt == null) return null;
        _currentBlock.Body.Insert(_currentBlock.Body.IndexOf(aStmt) + 1, collectionUpdateStmt);
        return collectionUpdateStmt;
    }
    
    private Statement? Mutate(Method method) {
        if (method.Body == null) return null;
        NameSegment? collection = null;
        if (_methodOutsIndex != -1 && method.Outs.Count > _methodOutsIndex) {
            collection = new NameSegment(_mutOrigin, method.Outs[_methodOutsIndex].Name, null);
        }
        if (collection == null) return null;
        
        _mutOrigin = method.EndToken;
        var collectionUpdateStmt = CreateCollectionUpdateStmt(collection);
        if (collectionUpdateStmt == null) return null;
        if (method.Body.Body[^1] is ReturnStmt) 
            method.Body.Body.RemoveAt(method.Body.Body.Count - 1);
        method.Body.Body.Add(collectionUpdateStmt);
        return collectionUpdateStmt;
    }

    private Statement? CreateCollectionUpdateStmt(NameSegment collection) {
        return collectionUpdateType switch {
            "fstElem" => CreateElementUpdateStmt(collection, true),
            "lstElem" => CreateElementUpdateStmt(collection, false),
            "copy" => CreateCollectionCopyStmt(collection),
            "compInit" => CreateCollectionComprehensionStmt(collection),
            _ => null,
        };
    }

    /// ---------------------------
    /// CUS-fstElem and CUS-lstElem
    /// ---------------------------
    private Statement? CreateElementUpdateStmt(NameSegment collection, bool firstElem) {
        return collectionTypeStr switch {
            _ when collectionTypeStr.StartsWith("seq<") => CreateSeqUpdateStmt(collection, firstElem),
            _ when collectionTypeStr.StartsWith("array<") => CreateArrayUpdateStmt(collection, firstElem),
            _ when collectionTypeStr.StartsWith("set<") => CreateSetUpdateStmt(collection),
            _ when collectionTypeStr.StartsWith("multiset<") => CreateMultisetUpdateStmt(collection),
            _ when collectionTypeStr.StartsWith("map<") => CreateMapUpdateStmt(collection),
            _ => null,
        };
    }

    private Statement CreateSeqUpdateStmt(NameSegment collection, bool firstElem) {
        var collectionIndex = CreateCollectionIndexExpr(collection, firstElem);
        var defaultValue = CreateDefaultArgValueExpr();
        var seqUpdateExpr = new SeqUpdateExpr(_mutOrigin, collection, collectionIndex, defaultValue);
        return new AssignStatement(_mutOrigin, [collection], [new ExprRhs(seqUpdateExpr)]);
    }
    
    private Statement CreateArrayUpdateStmt(NameSegment collection, bool firstElem) {
        var collectionIndex = CreateCollectionIndexExpr(collection, firstElem);
        var defaultValue = CreateDefaultArgValueExpr();
        var arraySelectExpr = new SeqSelectExpr(_mutOrigin, true, collection, collectionIndex, null);
        return new AssignStatement(_mutOrigin, [arraySelectExpr], [new ExprRhs(defaultValue)]);
    }

    private Expression CreateCollectionIndexExpr(NameSegment collection, bool firstElem) {
        Expression lengthExpr = collectionTypeStr.StartsWith("seq<")
            ? new UnaryOpExpr(_mutOrigin, UnaryOpExpr.Opcode.Cardinality, collection)
            : new ExprDotName(_mutOrigin, collection, new Name("Length"), null);
        return firstElem ? new LiteralExpr(_mutOrigin, 0) : 
            new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.Sub, lengthExpr, new LiteralExpr(_mutOrigin, 1));
    }

    private Statement CreateSetUpdateStmt(NameSegment collection) {
        var setElemSelectStmt = CreateUnorderedCollectionElemSelectStmt(collection, false);
        var elemSelectVar = new NameSegment(_mutOrigin, setElemSelectStmt.Locals[0].Name, null);
        var elemSelectSubset = new SetDisplayExpr(_mutOrigin, true, [elemSelectVar]);
        var setRemoveElemExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.Sub, collection, elemSelectSubset);
        var defaultValue = new SetDisplayExpr(_mutOrigin, true, [CreateDefaultArgValueExpr()]);
        var setUpdateExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.Add, setRemoveElemExpr, defaultValue);
        var setUpdateStmt = new AssignStatement(_mutOrigin, [collection], [new ExprRhs(setUpdateExpr)]);
        var emptySetExpr = new SetDisplayExpr(_mutOrigin, true, []);
        return CreateUnorderedCollectionNotEmptyStmt(collection, emptySetExpr, false, 
            new BlockStmt(_mutOrigin, [setElemSelectStmt, setUpdateStmt]));
    }

    private Statement CreateMultisetUpdateStmt(NameSegment collection) {
        var multisetElemSelectStmt = CreateUnorderedCollectionElemSelectStmt(collection, false);
        var elemSelectVar = new NameSegment(_mutOrigin, multisetElemSelectStmt.Locals[0].Name, null);
        var elemSelectSubset = new MultiSetDisplayExpr(_mutOrigin, [elemSelectVar]);
        var multisetRemoveElemExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.Sub, collection, elemSelectSubset);
        var defaultValue = new MultiSetDisplayExpr(_mutOrigin, [CreateDefaultArgValueExpr()]);
        var multisetUpdateExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.Add, multisetRemoveElemExpr, defaultValue);
        var multisetUpdateStmt = new AssignStatement(_mutOrigin, [collection], [new ExprRhs(multisetUpdateExpr)]);
        var emptyMultisetExpr = new MultiSetDisplayExpr(_mutOrigin, []);
        return CreateUnorderedCollectionNotEmptyStmt(collection, emptyMultisetExpr, false, 
            new BlockStmt(_mutOrigin, [multisetElemSelectStmt, multisetUpdateStmt]));
    }

    private Statement CreateMapUpdateStmt(NameSegment collection) {
        var mapElemSelectStmt = CreateUnorderedCollectionElemSelectStmt(collection, true);
        var elemSelectVar = new NameSegment(_mutOrigin, mapElemSelectStmt.Locals[0].Name, null);
        var defaultValue = CreateDefaultArgValueExpr();
        var mapUpdateExpr = new SeqUpdateExpr(_mutOrigin, collection, elemSelectVar, defaultValue);
        var mapUpdateStmt = new AssignStatement(_mutOrigin, [collection], [new ExprRhs(mapUpdateExpr)]);
        var emptyMapExpr = new SetDisplayExpr(_mutOrigin, true, []);
        return CreateUnorderedCollectionNotEmptyStmt(collection, emptyMapExpr, true, 
            new BlockStmt(_mutOrigin, [mapElemSelectStmt, mapUpdateStmt]));
    }

    private VarDeclStmt CreateUnorderedCollectionElemSelectStmt(NameSegment collection, bool isMap) {
        var arbitraryElemVar = new NameSegment(_mutOrigin, "arbitraryAuxVar'", null);
        var localVar = new LocalVariable(_mutOrigin, arbitraryElemVar.Name, null, false);
        Expression arbitraryElemSource = !isMap ? collection : 
            new ExprDotName(_mutOrigin, collection, new Name("Keys"), null);
        var arbitraryVarSelectExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.In, arbitraryElemVar, arbitraryElemSource);
        var varDeclAssign = new AssignSuchThatStmt(_mutOrigin, [arbitraryElemVar], arbitraryVarSelectExpr, null, null);
        return new VarDeclStmt(_mutOrigin, [localVar], varDeclAssign);
    }

    private IfStmt CreateUnorderedCollectionNotEmptyStmt(NameSegment collection, Expression emptyCollection, bool isMap, BlockStmt ifBody) {
        Expression collectionToCheck = !isMap ? collection : 
            new ExprDotName(_mutOrigin, collection, new Name("Keys"), null);
        var collectionIsNotEmptyExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.Neq,
            collectionToCheck, emptyCollection);
        return new IfStmt(_mutOrigin, false, collectionIsNotEmptyExpr, ifBody, null, null);
    }

    /// --------
    /// CUS-copy
    /// --------
    private Statement? CreateCollectionCopyStmt(NameSegment collection) {
        var args = collectionTypeStr.Split("<->");
        if (args.Length != 3) return null;
        var sourceCollection = new NameSegment(_mutOrigin, args[0], null);
        var lhsCollectionType = args[1];
        var rhsCollectionType = args[2];

        AssignmentRhs? copyAssignRhs = null;
        if (lhsCollectionType == rhsCollectionType) {
            copyAssignRhs = new ExprRhs(sourceCollection);
        } else if (lhsCollectionType.StartsWith("seq<") && rhsCollectionType.StartsWith("array<")) {
            copyAssignRhs = CreateSeqFromSubsequence(sourceCollection);
        } else if (lhsCollectionType.StartsWith("array<") && rhsCollectionType.StartsWith("seq<")) {
            copyAssignRhs = CreateArrayFromComprehensionExpr(sourceCollection, lhsCollectionType);
        } else if (lhsCollectionType.StartsWith("set<") && (rhsCollectionType.StartsWith("seq<") || rhsCollectionType.StartsWith("multiset<"))) {
            copyAssignRhs = CreateSetFromComprehensionExpr(sourceCollection, lhsCollectionType);
        } else if (lhsCollectionType.StartsWith("set<") && rhsCollectionType.StartsWith("map<")) {
            copyAssignRhs = CreateSetFromMapKeyValueSets(sourceCollection, rhsCollectionType);
        } else if (lhsCollectionType.StartsWith("multiset<")) {
            copyAssignRhs = CreateMultisetFromSet(sourceCollection, lhsCollectionType, rhsCollectionType);
        }
        return copyAssignRhs == null ? null : new AssignStatement(_mutOrigin, [collection], [copyAssignRhs]);
    }

    private ExprRhs CreateSeqFromSubsequence(NameSegment array) {
        var seqInitExpr = new SeqSelectExpr(_mutOrigin, false, array, null, null);
        return new ExprRhs(seqInitExpr);
    }

    private TypeRhs? CreateArrayFromComprehensionExpr(NameSegment collection, string lhsCollectionType) {
        var arrayType = CreateArgTypeFromStr(lhsCollectionType);
        if (arrayType == null) return null;
        var arrayDimensions = new UnaryOpExpr(_mutOrigin, UnaryOpExpr.Opcode.Cardinality, collection);
        var indexBoundVar = new BoundVar(_mutOrigin, "indexAuxVar'", new IntType());
        var indexVar = new NameSegment(_mutOrigin, "indexAuxVar'", null);
        var lowerBound = new LiteralExpr(_mutOrigin, 0);
        var rangeExpr = new ChainingExpression(_mutOrigin, [lowerBound, indexVar, arrayDimensions],
            [BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Lt], 
            [null, null], [null, null]);
        var element = new SeqSelectExpr(_mutOrigin, true, collection, indexVar, null);
        var arrayComprehension = new LambdaExpr(_mutOrigin, [indexBoundVar], 
            rangeExpr, new Specification<FrameExpression>(), element);
        return new TypeRhs(_mutOrigin, arrayType, [arrayDimensions], arrayComprehension);
    }

    private ExprRhs? CreateSetFromComprehensionExpr(NameSegment collection, string lhsCollectionType) {
        var setType = CreateArgTypeFromStr(lhsCollectionType);
        if (setType == null) return null;
        var elemBoundVar = new BoundVar(_mutOrigin, "elemAuxVar'", setType);
        var elemVar = new NameSegment(_mutOrigin, "elemAuxVar'", null);
        var elemVarId = new IdentifierExpr(_mutOrigin, "elemAuxVar'");
        var rangeExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.In, elemVar, collection);
        var setComprehension = new SetComprehension(_mutOrigin, true, [elemBoundVar], rangeExpr, elemVarId, null);
        return new ExprRhs(setComprehension);
    }

    private ExprRhs CreateSetFromMapKeyValueSets(NameSegment collection, string rhsCollectionType) {
        var isSourceMapKeys = rhsCollectionType.EndsWith("->");
        var keyValueSetField = new Name(_mutOrigin, isSourceMapKeys ? "Keys" : "Values");
        var mapKeyValueSet = new ExprDotName(_mutOrigin, collection, keyValueSetField, null);
        return new ExprRhs(mapKeyValueSet);
    }

    private ExprRhs? CreateMultisetFromSet(NameSegment collection, string lhsCollectionType, string rhsCollectionType) {
        var setSource = rhsCollectionType switch {
            _ when rhsCollectionType.StartsWith("seq<") => CreateSetFromComprehensionExpr(collection, lhsCollectionType)?.Expr,
            _ when rhsCollectionType.StartsWith("set<") => collection,
            _ when rhsCollectionType.StartsWith("map<") => CreateSetFromMapKeyValueSets(collection, rhsCollectionType).Expr,
            _ => null
        };
        if (setSource == null) return null;
        var multisetInitExpr = new MultiSetFormingExpr(_mutOrigin, setSource);
        return new ExprRhs(multisetInitExpr);
    }

    /// ------------
    /// CUS-compInit
    /// ------------
    private Statement? CreateCollectionComprehensionStmt(NameSegment collection) {
        AssignmentRhs? comprehensionExpr = collectionTypeStr switch {
            _ when collectionTypeStr.StartsWith("seq<") => CreateSeqComprehensionExpr(collection),
            _ when collectionTypeStr.StartsWith("array<") => CreateArrayComprehensionExpr(collection),
            _ when collectionTypeStr.StartsWith("set<") => CreateSetComprehensionExpr(collection),
            _ when collectionTypeStr.StartsWith("multiset<") => CreateMultisetComprehensionExpr(collection),
            _ when collectionTypeStr.StartsWith("map<") => CreateMapComprehensionExpr(collection),
            _ => null,
        };
        return comprehensionExpr == null ? null : new AssignStatement(null, [collection], [comprehensionExpr]);
    }

    private ExprRhs? CreateSeqComprehensionExpr(NameSegment collection) {
        var seqLengthExpr = new UnaryOpExpr(_mutOrigin, UnaryOpExpr.Opcode.Cardinality, collection);
        var seqInit = CreateComprehensionLambdaExpr();
        if (seqInit == null) return null;
        var seqComprehensionExpr = new SeqConstructionExpr(_mutOrigin, null, seqLengthExpr, seqInit);
        return new ExprRhs(seqComprehensionExpr);
    }

    private TypeRhs? CreateArrayComprehensionExpr(NameSegment collection) {
        var arrayType = CreateArgTypeFromStr(collectionTypeStr);
        var arrayDimensions = new ExprDotName(_mutOrigin, collection, new Name("Length"), null);
        var arrayComprehension = CreateComprehensionLambdaExpr();
        return arrayType == null || arrayComprehension == null ? null : 
            new TypeRhs(_mutOrigin, arrayType, [arrayDimensions], arrayComprehension);
    }

    private ExprRhs? CreateSetComprehensionExpr(NameSegment collection) {
        var setType = CreateArgTypeFromStr(collectionTypeStr);
        var elemBoundVar = new BoundVar(_mutOrigin, "elemAuxVar'", setType);
        var elemVar = new NameSegment(_mutOrigin, "elemAuxVar'", null);
        var elemVarId = new IdentifierExpr(_mutOrigin, "elemAuxVar'");
        var elementSeq = CreateSeqComprehensionExpr(collection)?.Expr;
        var rangeExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.In, elemVar, elementSeq);
        if (setType == null || elementSeq == null) 
            return null;
        var setComprehension = new SetComprehension(_mutOrigin, true, [elemBoundVar], rangeExpr, elemVarId, null);
        return new ExprRhs(setComprehension);
    }

    private ExprRhs? CreateMultisetComprehensionExpr(NameSegment collection) {
        var elementSet = CreateSetComprehensionExpr(collection)?.Expr;
        if (elementSet == null) return null;
        var multisetComprehension = new MultiSetFormingExpr(_mutOrigin, elementSet);
        return new ExprRhs(multisetComprehension);
    }

    private ExprRhs? CreateMapComprehensionExpr(NameSegment collection) {
        var mapKeyType = CreateArgTypeFromStr(collectionTypeStr);
        var elemBoundVar = new BoundVar(_mutOrigin, "elemAuxVar'", mapKeyType);
        var elemVar = new NameSegment(_mutOrigin, "elemAuxVar'", null);
        var elementSeq = CreateSeqComprehensionExpr(collection)?.Expr;
        var rangeExpr = new BinaryExpr(_mutOrigin, BinaryExpr.Opcode.In, elemVar, elementSeq);
        var elementValue = CreateDefaultComprehensionValueExpr(elemVar, true);
        if (mapKeyType == null || elementSeq == null || elementValue == null) 
            return null;
        var mapComprehension = new MapComprehension(_mutOrigin, true, [elemBoundVar], 
            rangeExpr, null, elementValue, null);
        return new ExprRhs(mapComprehension);
    }

    private LambdaExpr? CreateComprehensionLambdaExpr() {
        var indexBoundVar = new BoundVar(_mutOrigin, "indexAuxVar'", new IntType());
        var indexVar = new NameSegment(_mutOrigin, "indexAuxVar'", null);
        var element = CreateDefaultComprehensionValueExpr(indexVar);
        if (element == null) return null;
        return new LambdaExpr(_mutOrigin, [indexBoundVar], 
            null, new Specification<FrameExpression>(), element);
    }

    /// --------
    /// Utils
    /// --------
    private LiteralExpr? CreateDefaultArgValueExpr() {
        var firstIndex = collectionTypeStr.IndexOf('<') + 1;
        var lastIndex = collectionTypeStr.LastIndexOf('>');
        var strLength = lastIndex - firstIndex;
        var elementType = collectionTypeStr.Substring(firstIndex, strLength);
        if (collectionTypeStr.StartsWith("map<"))
            elementType = elementType.Split("-")[1];
        return elementType switch {
            "int" or "nat" => new LiteralExpr(_mutOrigin, 0),
            "real" => new LiteralExpr(_mutOrigin, BigDec.ZERO),
            _ when elementType.StartsWith("bv") => new LiteralExpr(_mutOrigin, BigInteger.Zero),
            "bool" => new LiteralExpr(_mutOrigin, false),
            "char" => new CharLiteralExpr(_mutOrigin, "0"),
            "string" => new StringLiteralExpr(_mutOrigin, "", false),
            _ => null
        };
    }
    
    private Expression? CreateDefaultComprehensionValueExpr(NameSegment indexVar, bool targetMapValue = false) {
        var firstIndex = collectionTypeStr.IndexOf('<') + 1;
        var lastIndex = collectionTypeStr.LastIndexOf('>');
        var strLength = lastIndex - firstIndex;
        var elementType = collectionTypeStr.Substring(firstIndex, strLength);
        if (collectionTypeStr.StartsWith("map<"))
            elementType = !targetMapValue ? elementType.Split("-")[0] : elementType.Split("-")[1];
        return elementType switch {
            "int" or "nat" => indexVar,
            "real" => new ConversionExpr(_mutOrigin, indexVar, new RealType()),
            _ when elementType.StartsWith("bv") => new LiteralExpr(_mutOrigin, BigInteger.Zero),
            "bool" => new LiteralExpr(_mutOrigin, false),
            "char" => new CharLiteralExpr(_mutOrigin, "0"),
            "string" => new StringLiteralExpr(_mutOrigin, "", false),
            _ => null
        };
    }

    private Type? CreateArgTypeFromStr(string type) {
        var firstIndex = type.IndexOf('<') + 1;
        var lastIndex = type.LastIndexOf('>');
        var strLength = lastIndex - firstIndex;
        type = type.Substring(firstIndex, strLength);
        if (collectionTypeStr.StartsWith("map<"))
            type = type.Split("-")[0];
        return type switch {
            "int" or "nat" => new IntType(),
            "real" => new RealType(),
            _ when type.StartsWith("bv") => new BitvectorType(null, int.Parse(type[2..])),
            "bool" => new BoolType(),
            "char" => new CharType(),
            "string" => new UserDefinedType(_mutOrigin, "string", []),
            _ => null
        };
    }

    private bool IsTarget(Statement stmt) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length != 3) return false;
        var startPosition = int.Parse(positions[0]);
        var endPosition = int.Parse(positions[1]);
        
        if (stmt.StartToken.pos == startPosition && stmt.EndToken.pos == endPosition) {
            _assignLhsIndex = int.Parse(positions[2]);
            return true;
        }
        return false;
    }
    
    private bool IsTarget(Method method) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length != 2) return false;
        var position = int.Parse(positions[0]);
        
        if (method.EndToken.pos == position) {
            _methodOutsIndex = int.Parse(positions[1]);
            return true;
        }
        return false;
    }

    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void HandleMethod(Method method) {
        if (IsTarget(method)) {
            TargetStatement = Mutate(method);
            if (TargetStatement != null) {
                MutantGenerator.NumMutations++;
                MutantGenerator.MutatedNodes.Add(TargetStatement);
                return;
            }
        }
        base.HandleMethod(method);
    }

    protected override void HandleBlock(BlockStmt blockStmt) {
        var prevCurrentBlock = _currentBlock;
        _currentBlock = blockStmt;
        base.HandleBlock(blockStmt);
        _currentBlock = prevCurrentBlock;
    }

    protected override void VisitStatement(AssignStatement aStmt) {
        if (IsTarget(aStmt)) {
            TargetStatement = Mutate(aStmt);
            if (TargetStatement != null) {
                MutantGenerator.NumMutations++;
                MutantGenerator.MutatedNodes.Add(TargetStatement);
                return; 
            }
        }
        base.VisitStatement(aStmt);
    }
}