using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator(int mutationTargetPos, ErrorReporter reporter) : Visitor.Visitor(mutationTargetPos, reporter)
{
    public void Mutate(ModuleDefinition topNode) {
        base.Find(topNode);
    }
}