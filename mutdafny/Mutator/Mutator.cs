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
}