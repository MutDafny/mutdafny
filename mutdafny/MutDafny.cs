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
    private string MutationTargetURI { get; set; } = "";
    private int NumMutations { get; set; } = -1;
    private string MutationTargetPos { get; set; } = "";
    private string MutationOperator { get; set; } = "";
    private string? MutationArg { get; set; }
    
    public override void ParseArguments(string[] args) {
        if (args.Length == 0) return;
        if (args[0] == "scan") {
            _scan = true;
            ParseScanArguments(args);
        } 
        else if (args[0] == "mut" && args.Length >= 2) {
            _mutate = true;
            ParseMutArguments(args);
        } else if (args[0] == "analyze") {
            _analyze = true;
            if (args.Length == 1) return;
            MutationTargetURI = args[1];
        }
    }

    private void ParseScanArguments(string[] args) {
        if (args.Length == 1) return;
        if (args[1].EndsWith(".dfy")) {
            MutationTargetURI = args[1];
            if (args.Length == 2) return;
            OperatorsInUse = new List<string>(args[2..]);   
        } else { 
            OperatorsInUse = new List<string>(args[1..]);   
        }
    }

    private void ParseMutArguments(string[] args) {
        if (args.Length == 2) {
            NumMutations = int.Parse(args[1]);
        } else {
            MutationTargetPos = args[1];
            MutationOperator = args[2];
            MutationArg = args.Length switch {
                3 => null,
                4 => args[3],
                _ => string.Join(" ", new List<string>(args[new Range(3, args.Length)]))
            };
        }
    }

    public override Rewriter[] GetRewriters(ErrorReporter reporter) {
        return _mutate ? 
            [new MutantGenerator(NumMutations, MutationTargetPos, MutationOperator, MutationArg, reporter)] : 
            (_scan ? [new MutationTargetScanner(MutationTargetURI, OperatorsInUse, reporter)] : 
             _analyze ? [new ProgramAnalyzer(MutationTargetURI, reporter)] : []);
    }
}

public class MutationTargetScanner(string mutationTargetURI, List<string> operatorsInUse, ErrorReporter reporter) : Rewriter(reporter)
{
    public static bool FirstCall = true;
    
    public override void PreResolve(ModuleDefinition module) {
        var specHelperFinder = new SpecHelperFinder(Reporter);
        specHelperFinder.Find(module);
        
        var targetScanner = new PreResolveTargetScanner(mutationTargetURI, operatorsInUse, Reporter);
        targetScanner.Find(module);
        targetScanner.ExportTargets();
    }

    public override void PostResolve(ModuleDefinition module) {
        var targetScanner = new PostResolveTargetScanner(mutationTargetURI, operatorsInUse, Reporter);
        targetScanner.Find(module);
        targetScanner.ExportTargets();
        FirstCall = false;
    }
}

public class MutantGenerator(int numMutations, string mutationTargetPos, string mutationOperator, string? mutationArg, ErrorReporter reporter) : Rewriter(reporter)
{
    public override void PreResolve(ModuleDefinition module) {
        if (numMutations == -1) {
            GenerateMutant(module, mutationTargetPos, mutationOperator, mutationArg); 
        } else {
          // TODO  
        }
    }

    private void GenerateMutant(ModuleDefinition module, string mutationTargetPos, string mutationOperator, string? mutationArg) {
        if (mutationOperator == "VDL" || mutationOperator == "ODL") {
            var specHelperFinder = new SpecHelperFinder(Reporter);
            specHelperFinder.Find(module);
        }
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
        // TODO: change for multiple mutations
        filename += mutationArg != null ? 
            $"_{mutationTargetPos}_{mutationOperator}_{mutationArg}.dfy" : 
            $"_{mutationTargetPos}_{mutationOperator}.dfy";
        File.WriteAllText(filename, programText);
    }
}

public class ProgramAnalyzer(string mutationTargetURI, ErrorReporter reporter) : Rewriter(reporter)
{
    public static bool FirstCall = true;

    public override void PreResolve(ModuleDefinition module) {
        var analyzer = new Analyzer(mutationTargetURI, Reporter);
        analyzer.Find(module);
        analyzer.ExportData();
        FirstCall = false;
    }
}