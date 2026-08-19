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
        
        var collectionUpdateStmt = CreateCollectionUpdateStmt(collection);
        if (collectionUpdateStmt == null) return null;
        _currentBlock.Body.Insert(_currentBlock.Body.IndexOf(aStmt) + 1, collectionUpdateStmt);
        return collectionUpdateStmt;
    }
    
    private Statement? Mutate(Method method) {
        if (method.Body == null) return null;
        NameSegment? collection = null;
        if (_methodOutsIndex != -1 && method.Outs.Count > _methodOutsIndex) {
            collection = new NameSegment(null, method.Outs[_methodOutsIndex].Name, null);
        }
        if (collection == null) return null;
        
        var collectionUpdateStmt = CreateCollectionUpdateStmt(collection);
        if (collectionUpdateStmt == null) return null;
        method.Body.Body.Add(collectionUpdateStmt);
        return collectionUpdateStmt;
    }

    private Statement? CreateCollectionUpdateStmt(NameSegment collection) {
        return collectionUpdateType switch {
            "fstElem" => CreateElementUpdateStmt(collection, true),
            "lstElem" => CreateElementUpdateStmt(collection, false),
            _ => null,
        };
    }

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
        var defaultValue = CreateDefaultValueExpr();
        var seqUpdateExpr = new SeqUpdateExpr(null, collection, collectionIndex, defaultValue);
        return new AssignStatement(null, [collection], [new ExprRhs(seqUpdateExpr)]);
    }
    
    private Statement CreateArrayUpdateStmt(NameSegment collection, bool firstElem) {
        var collectionIndex = CreateCollectionIndexExpr(collection, firstElem);
        var defaultValue = CreateDefaultValueExpr();
        var arraySelectExpr = new SeqSelectExpr(null, true, collection, collectionIndex, null);
        return new AssignStatement(null, [arraySelectExpr], [new ExprRhs(defaultValue)]);
    }

    private Expression CreateCollectionIndexExpr(NameSegment collection, bool firstElem) {
        Expression lengthExpr = collectionTypeStr.StartsWith("seq<")
            ? new UnaryOpExpr(null, UnaryOpExpr.Opcode.Cardinality, collection)
            : new ExprDotName(null, collection, new Name("Length"), null);
        return firstElem ? new LiteralExpr(null, 0) : 
            new BinaryExpr(null, BinaryExpr.Opcode.Sub, lengthExpr, new LiteralExpr(null, 1));
    }

    private Statement CreateSetUpdateStmt(NameSegment collection) {
        var setElemSelectStmt = CreateUnorderedCollectionElemSelectStmt(collection, false);
        var elemSelectVar = new NameSegment(null, setElemSelectStmt.Locals[0].Name, null);
        var elemSelectSubset = new SetDisplayExpr(null, true, [elemSelectVar]);
        var setRemoveElemExpr = new BinaryExpr(null, BinaryExpr.Opcode.Sub, collection, elemSelectSubset);
        var defaultValue = new SetDisplayExpr(null, true, [CreateDefaultValueExpr()]);
        var setUpdateExpr = new BinaryExpr(null, BinaryExpr.Opcode.Add, setRemoveElemExpr, defaultValue);
        var setUpdateStmt = new AssignStatement(null, [collection], [new ExprRhs(setUpdateExpr)]);
        var emptySetExpr = new SetDisplayExpr(null, true, []);
        return CreateUnorderedCollectionNotEmptyStmt(collection, emptySetExpr, false, 
            new BlockStmt(null, [setElemSelectStmt, setUpdateStmt]));
    }

    private Statement CreateMultisetUpdateStmt(NameSegment collection) {
        var multisetElemSelectStmt = CreateUnorderedCollectionElemSelectStmt(collection, false);
        var elemSelectVar = new NameSegment(null, multisetElemSelectStmt.Locals[0].Name, null);
        var elemSelectSubset = new MultiSetDisplayExpr(null, [elemSelectVar]);
        var multisetRemoveElemExpr = new BinaryExpr(null, BinaryExpr.Opcode.Sub, collection, elemSelectSubset);
        var defaultValue = new MultiSetDisplayExpr(null, [CreateDefaultValueExpr()]);
        var multisetUpdateExpr = new BinaryExpr(null, BinaryExpr.Opcode.Add, multisetRemoveElemExpr, defaultValue);
        var multisetUpdateStmt = new AssignStatement(null, [collection], [new ExprRhs(multisetUpdateExpr)]);
        var emptyMultisetExpr = new MultiSetDisplayExpr(null, []);
        return CreateUnorderedCollectionNotEmptyStmt(collection, emptyMultisetExpr, false, 
            new BlockStmt(null, [multisetElemSelectStmt, multisetUpdateStmt]));
    }

    private Statement CreateMapUpdateStmt(NameSegment collection) {
        var mapElemSelectStmt = CreateUnorderedCollectionElemSelectStmt(collection, true);
        var elemSelectVar = new NameSegment(null, mapElemSelectStmt.Locals[0].Name, null);
        var defaultValue = CreateDefaultValueExpr();
        var mapUpdateExpr = new SeqUpdateExpr(null, collection, elemSelectVar, defaultValue);
        var mapUpdateStmt = new AssignStatement(null, [collection], [new ExprRhs(mapUpdateExpr)]);
        var emptyMapExpr = new SetDisplayExpr(null, true, []);
        return CreateUnorderedCollectionNotEmptyStmt(collection, emptyMapExpr, true, 
            new BlockStmt(null, [mapElemSelectStmt, mapUpdateStmt]));
    }

    private VarDeclStmt CreateUnorderedCollectionElemSelectStmt(NameSegment collection, bool isMap) {
        var arbitraryElemVar = new NameSegment(null, "arbitraryAuxVar'", null);
        var localVar = new LocalVariable(null, arbitraryElemVar.Name, null, false);
        Expression arbitraryElemSource = !isMap ? collection : 
            new ExprDotName(null, collection, new Name("Keys"), null);
        var arbitraryVarSelectExpr = new BinaryExpr(null, BinaryExpr.Opcode.In, arbitraryElemVar, arbitraryElemSource);
        var varDeclAssign = new AssignSuchThatStmt(null, [arbitraryElemVar], arbitraryVarSelectExpr, null, null);
        return new VarDeclStmt(null, [localVar], varDeclAssign);
    }

    private IfStmt CreateUnorderedCollectionNotEmptyStmt(NameSegment collection, Expression emptyCollection, bool isMap, BlockStmt ifBody) {
        Expression collectionToCheck = !isMap ? collection : 
            new ExprDotName(null, collection, new Name("Keys"), null);
        var collectionIsNotEmptyExpr = new BinaryExpr(null, BinaryExpr.Opcode.Neq,
            collectionToCheck, emptyCollection);
        return new IfStmt(null, false, collectionIsNotEmptyExpr, ifBody, null, null);
    }

    private LiteralExpr? CreateDefaultValueExpr() {
        var firstIndex = collectionTypeStr.IndexOf('<') + 1;
        var lastIndex = collectionTypeStr.LastIndexOf('>');
        var strLength = lastIndex - firstIndex;
        var elementType = collectionTypeStr.Substring(firstIndex, strLength);
        if (collectionTypeStr.StartsWith("map<"))
            elementType = elementType.Split("-")[1];
        return elementType switch {
            "int" or "nat" => new LiteralExpr(null, 0),
            "real" => new LiteralExpr(null, BigDec.ZERO),
            _ when elementType.StartsWith("bv") => new LiteralExpr(null, BigInteger.Zero),
            "bool" => new LiteralExpr(null, false),
            "char" => new CharLiteralExpr(null, "0"),
            "string" => new StringLiteralExpr(null, "", false),
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