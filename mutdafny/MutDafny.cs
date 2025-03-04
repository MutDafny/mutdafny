using Microsoft.Dafny;
using Microsoft.Dafny.Plugins;
using MutDafny.Mutator;
using MutDafny.TargetFinder;
using PluginConfiguration = Microsoft.Dafny.LanguageServer.Plugins.PluginConfiguration;

namespace MutDafny;

public class MutDafny : PluginConfiguration
{
    private bool _mutate = true;

    private int MutationTargetPos { get; set; }
    private string MutationType { get; set; }
    private string? MutationTypeArg { get; set; }

    public override void ParseArguments(string[] args)
    {
        if (args.Length < 2) {
            _mutate = false;
            return;
        }
        MutationTargetPos = int.Parse(args[0]);
        MutationType = args[1];
        MutationTypeArg = args.Length > 2 ? args[2] : null;
    }

    public override Rewriter[] GetRewriters(ErrorReporter reporter)
    {
        return _mutate ? 
            [new MutantGenerator(MutationTargetPos, MutationType, MutationTypeArg, reporter)] : 
            [];
    }
}

public class MutantGenerator(int mutationTargetPos, string mutationType, string? mutationTypeArg, ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module)
    {
        // TODO: use different finder/mutator according to type of operator
        var targetFinder = new BinaryOpTargetFinder(mutationTargetPos, Reporter);
        targetFinder.Find(module);
        var target = TargetFinder.TargetFinder.TargetStatement;
        if (target == null) return;
        
        if (mutationTypeArg == null) return; // TODO: only if we're dealing with binary op mutation
        var mutator = new BinaryOpMutator(mutationTargetPos, mutationTypeArg, Reporter);
        mutator.Mutate(target);
    }
}