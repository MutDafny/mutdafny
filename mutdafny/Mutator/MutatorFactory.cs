using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MutatorFactory(ErrorReporter reporter)
{
    public Mutator? Create(int mutationTargetPos, string mutationType, string? mutationArg) {
        switch (mutationType) {
            case "BOR":
                return mutationArg == null ? null :
                    new BinaryOpMutator(mutationTargetPos, mutationArg, reporter);
            case "BBR":
                return mutationArg == null ? null :
                    new BinaryOpBoolMutator(mutationTargetPos, mutationArg, reporter);
        }
        return null;
    }
}