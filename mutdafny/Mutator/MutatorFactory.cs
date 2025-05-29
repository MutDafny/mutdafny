using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class MutatorFactory(ErrorReporter reporter)
{
    public Mutator? Create(string mutationTargetPos, string mutationOperator, string? mutationArg)
    {
        return mutationOperator switch {
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
            "VER" => mutationArg == null ? null :
                new VariableExprReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "LSR" => mutationArg == null ? null : 
                new LoopStmtReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "LBI" => new BreakInsertionMutator(mutationTargetPos, reporter),
            "MCR" => mutationArg == null ? 
                new MethodCallReplacementMutator(mutationTargetPos, "", reporter) :
                new MethodCallReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "SAR" => mutationArg == null ? null :
                new SwitchArgMutator(mutationTargetPos, mutationArg, reporter),
            "CIR" => mutationArg == null ? 
                new CollectionInitReplacementMutator(mutationTargetPos, "", reporter) :
                new CollectionInitReplacementMutator(mutationTargetPos, mutationArg, reporter), 
            "CBR" => new CaseBlockReplacementMutator(mutationTargetPos, reporter),
            "DCR" => mutationArg == null ? null :
                new DatatypeCtorReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "SDL" => new StmtDeletionMutator(mutationTargetPos, reporter),
            "VDL" => mutationArg == null ? null :
                new VariableDeletionMutator(mutationTargetPos, mutationArg, reporter),
            "ODL" => mutationArg == null ? null :
                new OperatorDeletionMutator(mutationTargetPos, mutationArg, reporter),
            "THI" => new ThisKeywordInsertionMutator(mutationTargetPos, reporter),
            "THD" => new ThisKeywordDeletionMutator(mutationTargetPos, reporter),
            "AMR" => mutationArg == null ? null :
                new MethodBodyReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "MMR" => mutationArg == null ? null :
                new MethodBodyReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "FAR" => mutationArg == null ? null :
                new FieldAccessReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "PRV" => mutationArg == null ? null :
                new VariableExprReplacementMutator(mutationTargetPos, mutationArg, reporter),
            "SWS" => new SwitchStmtMutator(mutationTargetPos, reporter),
            _ => null
        };
    }
}