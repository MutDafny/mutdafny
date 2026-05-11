using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator(string mutationTargetPos, ErrorReporter reporter) : Visitor.Visitor(mutationTargetPos, reporter)
{
    public void Mutate(Program program) {
        base.Find(program);
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