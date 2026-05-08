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
    
    public override void PreResolve(Program program) {
        // save original code but post serialization to perform diffs
        StoreProgram(program);
    }
    
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
    
    private void StoreProgram(Program program) {
        var stringWriter = new StringWriter();
        var printer = new Printer(stringWriter, program.Options, PrintModes.Serialization);
        printer.PrintProgram(program, false);
        var programText = stringWriter.ToString();
        
        var filename = Path.GetFileNameWithoutExtension(program.Name) + ".dfy";
        Directory.CreateDirectory("original");
        File.WriteAllText(Path.Combine("original", filename), programText);
    }
}

public class MutantGenerator(int numMutations, string mutationTargetPos, string mutationOperator, string? mutationArg, ErrorReporter reporter) : Rewriter(reporter)
{
    public static List<Node> MutatedNodes { get; private set; } = [];
    public static int NumMutations = 0; // incremented upon mutating in child mutator classes
    private static bool _generatedAllMutations = true;
    private string _mutationTargetPos = mutationTargetPos;
    private string _mutationOperator = mutationOperator;
    private string? _mutationArg = mutationArg;
    private readonly List<(string, string, string)> _usedTargets = [];
    
    public override void PreResolve(Program program) {
        if (numMutations == -1) {
            MutateProgram(program);
        } else {
            var allTargets = ImportTargets();
            var toTryTargets = new List<(string, string, string)>(allTargets); // copy of allTargets
            var rand = new Random();   
            while (NumMutations < numMutations && toTryTargets.Count != 0) {
                var initialCount = NumMutations;
                var targetIdx = rand.Next(toTryTargets.Count);
                _mutationTargetPos = toTryTargets[targetIdx].Item1;
                _mutationOperator = toTryTargets[targetIdx].Item2;
                _mutationArg = toTryTargets[targetIdx].Item3;
                MutateProgram(program);

                if (initialCount < NumMutations)
                    _usedTargets.Add((_mutationTargetPos, _mutationOperator, _mutationArg));
                toTryTargets.RemoveAt(targetIdx);
            }
            
            // check if expected number of mutations was reached
            if (NumMutations != numMutations)
                _generatedAllMutations = false;
            ExportUpdatedTargets(allTargets);
        }
        StoreProgram(program);
    }

    private void MutateProgram(Program program) {
        if (_mutationOperator == "VDL" || _mutationOperator == "ODL") {
            var specHelperFinder = new SpecHelperFinder(Reporter);
            specHelperFinder.Find(program);
        }
        var mutatorFactory = new MutatorFactory(Reporter);
        var mutator = mutatorFactory.Create(_mutationTargetPos, _mutationOperator, _mutationArg);
        mutator?.Mutate(program);
    }

    private List<(string, string, string)> ImportTargets() {
        if (!File.Exists("targets.csv")) return [];
        
        var targets = new List<(string, string, string)>();
        var lines = File.ReadAllLines("targets.csv");
        foreach (var line in lines) {
            var components = line.Split(',');
            if (components.Length < 2)
                continue;

            var mutationPos = components[0];
            var mutationOp = components[1];
            var mutationArg = components.Length > 2 ? components[2] : "";
            targets.Add((mutationPos, mutationOp, mutationArg));
        }
        return targets;
    }
    
    // Rewrites the targets that haven't yet been used to external control file
    private void ExportUpdatedTargets(List<(string, string, string)> allTargets) {
        using StreamWriter sw = File.CreateText("targets.csv");
        foreach (var target in allTargets) {
            if (_generatedAllMutations && _usedTargets.Contains(target)) continue;
            var line = target.Item1 + "," + target.Item2 + "," + target.Item3;
            sw.WriteLine(line);
        }
    }
    
    private void StoreProgram(Program program) {
        if (!_generatedAllMutations) return;
        
        var stringWriter = new StringWriter();
        var printer = new Printer(stringWriter, program.Options, PrintModes.Serialization);
        printer.PrintProgram(program, false);
        var programText = stringWriter.ToString();

        var filename = Path.GetFileNameWithoutExtension(program.Name);
        if (numMutations == -1) {
            filename += _mutationArg != "" ? 
                $"__{_mutationTargetPos}_{_mutationOperator}_{_mutationArg}.dfy" : 
                $"__{_mutationTargetPos}_{_mutationOperator}.dfy";   
        } else {
            foreach (var target in _usedTargets) {
                filename += target.Item3 != "" ? 
                    $"__{target.Item1}_{target.Item2}_{target.Item3}" : 
                    $"__{target.Item1}_{target.Item2}";
            }
            filename += ".dfy";
        }
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