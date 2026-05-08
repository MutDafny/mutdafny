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

    protected bool ContainsMutatedChildren(Node nodeUnderMut) {
        foreach (var child in nodeUnderMut.Children) {
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