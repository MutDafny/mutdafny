using Microsoft.Dafny;
using Microsoft.Dafny.Plugins;
using MutDafny.Mutator;
using MutDafny.Visitor;
using PluginConfiguration = Microsoft.Dafny.LanguageServer.Plugins.PluginConfiguration;

namespace MutDafny;

public class MutDafny : PluginConfiguration
{
    private bool _mutate;
    private bool _scan;

    private string MutationTargetPos { get; set; } = "";
    private string MutationType { get; set; } = "";
    private string? MutationTypeArg { get; set; }

    public override void ParseArguments(string[] args) {
        if (args.Length == 0) {
            _scan = true;
        } else if (args.Length >= 2) {
            _mutate = true;
            MutationTargetPos = args[0];
            MutationType = args[1];
            MutationTypeArg = args.Length > 2 ? args[2] : null;   
        }
    }

    public override Rewriter[] GetRewriters(ErrorReporter reporter)
    {
        return _mutate ? 
            [new MutantGenerator(MutationTargetPos, MutationType, MutationTypeArg, reporter)] : 
            (_scan ? [new MutationTargetScanner(reporter)] : []);
    }
}

public class MutationTargetScanner(ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module) {
        var specHelperFinder = new SpecHelperFinder(Reporter);
        specHelperFinder.Find(module);
        
        var targetScanner = new PreResolveTargetScanner(Reporter);
        targetScanner.Find(module);
        targetScanner.ExportTargets();
    }

    public override void PostResolve(ModuleDefinition module) {
        var targetScanner = new PostResolveTargetScanner(Reporter);
        targetScanner.Find(module);
        targetScanner.ExportTargets();
    }
}

public class MutantGenerator(string mutationTargetPos, string mutationType, string? mutationTypeArg, ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module) {
        var specHelperFinder = new SpecHelperFinder(Reporter);
        specHelperFinder.Find(module);
        
        var mutatorFactory = new MutatorFactory(Reporter);
        var mutator = mutatorFactory.Create(mutationTargetPos, mutationType, mutationTypeArg);
        mutator?.Mutate(module);
    }

    public override void PostResolve(Program program) {
        var stringWriter = new StringWriter();
        var printer = new Printer(stringWriter, program.Options, PrintModes.Serialization);
        printer.PrintProgram(program, false);
        var programText = stringWriter.ToString();

        var filename = Path.GetFileNameWithoutExtension(program.Name);
        filename += mutationTypeArg != null ? 
            $"_{mutationTargetPos}_{mutationType}_{mutationTypeArg}.dfy" : 
            $"_{mutationTargetPos}_{mutationType}.dfy";
        File.WriteAllText(filename, programText);
    }
}