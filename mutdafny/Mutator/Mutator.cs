using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator(string mutationTargetPos, ErrorReporter reporter) : Visitor.Visitor(mutationTargetPos, reporter)
{
    public void Mutate(ModuleDefinition topNode) {
        base.Find(topNode);
    }
}