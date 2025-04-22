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
            "LVR" => mutationArg == null ? 
                new LiteralValueReplacementMutator(mutationTargetPos, "", reporter) :
                new LiteralValueReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "EVR" => mutationArg == null ? null :
                new ExprValueReplacementMutator(mutationTargetPos, mutationArg, reporter),
            _ => null
        };
    }
}