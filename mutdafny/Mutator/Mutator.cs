using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator(string mutationTargetPos, ErrorReporter reporter, bool multipleModule = false) 
    : Visitor.Visitor(mutationTargetPos, reporter, multipleModule)
{
    public void Mutate(ModuleDefinition topNode) {
        base.Find(topNode);
    }
}