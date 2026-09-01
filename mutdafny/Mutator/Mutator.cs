using System.Numerics;
using Microsoft.BaseTypes;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator(string mutationTargetPos, ErrorReporter reporter) : Visitor.Visitor(mutationTargetPos, reporter)
{
    public void Mutate(Program program) {
        base.Find(program);
    }

    protected static Expression CreateDefaultExpression(string type, IOrigin origin) {
        return type switch {
            "int" or "nat" => new LiteralExpr(origin, 0),
            "real" => new LiteralExpr(origin, BigDec.ZERO),
            "bv" => new LiteralExpr(origin, BigInteger.Zero),
            "bool" => new LiteralExpr(origin, false),
            "char" => new CharLiteralExpr(origin, "0"),
            "string" => new StringLiteralExpr(origin, "", false),
            "set" => new SetDisplayExpr(origin, true, []),
            "multiset" => new MultiSetDisplayExpr(origin, []),
            "seq" => new SeqDisplayExpr(origin, []),
            "map" => new MapDisplayExpr(origin, true, []),
            _ when type.StartsWith("datatype:") => new NameSegment(origin, type["datatype:".Length..], null),
            _ => new LiteralExpr(origin, null)
        };
    }
    
    protected void Mutate(ModuleDefinition module) {
        base.Find(module);
    }

    protected bool AlreadyMutated(Node nodeUnderMut) {
        return MutantGenerator.MutatedNodes.Contains(nodeUnderMut);
    }

    protected bool ContainsMutatedChildren(Node? nodeUnderMut) {
        if (nodeUnderMut == null) return false;
        var children = new List<INode>(nodeUnderMut.Children);
        if (nodeUnderMut is ParensExpression parensExpr) children.Add(parensExpr.E);
        
        foreach (var child in children) {
            if (child is not Node childNode) continue;
            if (MutantGenerator.MutatedNodes.Contains(child))
                return true;
            if (ContainsMutatedChildren(childNode))
                return true;
        }
        return false;
    }

    protected void ForbidChildrenMutation(Node mutatedNode) {
        foreach (var child in mutatedNode.Children) {
            if (child is not Node childNode) continue; 
            MutantGenerator.MutatedNodes.Add(childNode);
        }
    }
}