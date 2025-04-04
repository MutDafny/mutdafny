using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MutatorFactory(ErrorReporter reporter)
{
    public Mutator? Create(string mutationTargetPos, string mutationType, string? mutationArg)
    {
        return mutationType switch {
            "BOR" => mutationArg == null ? null : 
                new BinaryOpMutator(mutationTargetPos, mutationArg, reporter),
            "BBR" => mutationArg == null ? null : 
                new BinaryOpBoolMutator(mutationTargetPos, mutationArg, reporter),
            "UOI" => mutationArg == null ? null : 
                new UnaryOpInsertionMutator(mutationTargetPos, mutationArg, reporter),
            "UOD" => new UnaryOpDeletionMutator(mutationTargetPos, reporter),
            _ => null
        };
    }
}