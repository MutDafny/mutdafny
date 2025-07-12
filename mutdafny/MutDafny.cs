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
    private bool _analyze;

    private List<string> OperatorsInUse { get; set; } = [];
    private string MutationTargetPos { get; set; } = "";
    private string MutationOperator { get; set; } = "";
    private string? MutationArg { get; set; }
    
    public override void ParseArguments(string[] args) {
        if (args.Length == 0) return;
        if (args[0] == "scan") {
            _scan = true;
            if (args.Length == 1) return;
            OperatorsInUse = new List<string>(args[1..]);
        } 
        else if (args[0] == "mut" && args.Length >= 3) {
            _mutate = true;
            MutationTargetPos = args[1];
            MutationOperator = args[2];
            MutationArg = args.Length switch {
                3 => null,
                4 => args[3],
                _ => string.Join(" ", new List<string>(args[new Range(3, args.Length)]))
            };
        } else if (args[0] == "analyze") {
            _analyze = true;
        }
    }

    public override Rewriter[] GetRewriters(ErrorReporter reporter) {
        return _mutate ? 
            [new MutantGenerator(MutationTargetPos, MutationOperator, MutationArg, reporter)] : 
            (_scan ? [new MutationTargetScanner(OperatorsInUse, reporter)] : 
             _analyze ? [new Analyzer(reporter)] : []);
    }
}

public class MutationTargetScanner(List<string> operatorsInUse, ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module) {
        var specHelperFinder = new SpecHelperFinder(Reporter);
        specHelperFinder.Find(module);
        
        var targetScanner = new PreResolveTargetScanner(operatorsInUse, Reporter);
        targetScanner.Find(module);
        targetScanner.ExportTargets();
    }

    public override void PostResolve(ModuleDefinition module) {
        var targetScanner = new PostResolveTargetScanner(operatorsInUse, Reporter);
        targetScanner.Find(module);
        targetScanner.ExportTargets();
    }
}

public class MutantGenerator(string mutationTargetPos, string mutationOperator, string? mutationArg, ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module) {
        if (mutationOperator == "VDL" || mutationOperator == "ODL") return;
        var mutatorFactory = new MutatorFactory(Reporter);
        var mutator = mutatorFactory.Create(mutationTargetPos, mutationOperator, mutationArg);
        mutator?.Mutate(module);
    }

    public override void PostResolve(ModuleDefinition module) {
        if (mutationOperator != "VDL" && mutationOperator != "ODL") return;
        var mutatorFactory = new MutatorFactory(Reporter);
        var mutator = mutatorFactory.Create(mutationTargetPos, mutationOperator, mutationArg);
        mutator?.Mutate(module);
    }

    public override void PostResolve(Program program) {
        // save mutant
        var stringWriter = new StringWriter();
        var printer = new Printer(stringWriter, program.Options, PrintModes.Serialization);
        printer.PrintProgram(program, false);
        var programText = stringWriter.ToString();

        var filename = Path.GetFileNameWithoutExtension(program.Name);
        filename += mutationArg != null ? 
            $"_{mutationTargetPos}_{mutationOperator}_{mutationArg}.dfy" : 
            $"_{mutationTargetPos}_{mutationOperator}.dfy";
        File.WriteAllText(filename, programText);
    }
}

public class Analyzer(ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module) {
        var analyzer = new Visitor.Analyzer(reporter);
        analyzer.Find(module);
        analyzer.ExportData();
    }
}