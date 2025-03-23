using System.Text;
using Microsoft.Dafny;

namespace MutDafny.Visitor;

public class TargetScanner: Visitor
{
    private List<(int, string, string)> Targets { get; } = [];
    private readonly Dictionary<BinaryExpr.Opcode, List<BinaryExpr.Opcode>> _replacementList;

    public TargetScanner(ErrorReporter reporter): base(-1, reporter)
    {
       _replacementList = new Dictionary<BinaryExpr.Opcode, List<BinaryExpr.Opcode>> {
           // arithmetic operators
           { BinaryExpr.Opcode.Add, [BinaryExpr.Opcode.Sub, BinaryExpr.Opcode.Mul] },
           { BinaryExpr.Opcode.Sub, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Mul] },
           { BinaryExpr.Opcode.Mul, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Sub] },
           { BinaryExpr.Opcode.Div, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Sub, BinaryExpr.Opcode.Mul, BinaryExpr.Opcode.Mod] },
           { BinaryExpr.Opcode.Mod, [BinaryExpr.Opcode.Add, BinaryExpr.Opcode.Sub, BinaryExpr.Opcode.Mul, BinaryExpr.Opcode.Div] },
           // relational operators
           { BinaryExpr.Opcode.Eq, [BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, 
               BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Neq, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, 
               BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Lt, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, 
               BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Le, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Gt, 
               BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Gt, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, 
               BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Ge, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, 
               BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Iff, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, 
               BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Imp, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, 
               BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Exp] },
           { BinaryExpr.Opcode.Exp, [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, 
               BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge, BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp] },
           // conditional operators
           { BinaryExpr.Opcode.And, [BinaryExpr.Opcode.Or] },
           { BinaryExpr.Opcode.Or, [BinaryExpr.Opcode.And] },
           // logical operators
           { BinaryExpr.Opcode.BitwiseAnd, [BinaryExpr.Opcode.BitwiseOr, BinaryExpr.Opcode.BitwiseXor] },
           { BinaryExpr.Opcode.BitwiseOr, [BinaryExpr.Opcode.BitwiseAnd, BinaryExpr.Opcode.BitwiseXor] },
           { BinaryExpr.Opcode.BitwiseXor, [BinaryExpr.Opcode.BitwiseAnd, BinaryExpr.Opcode.BitwiseOr] },
           // shift operators
           { BinaryExpr.Opcode.LeftShift, [BinaryExpr.Opcode.RightShift] },
           { BinaryExpr.Opcode.RightShift, [BinaryExpr.Opcode.LeftShift] },
       }; 
    }
    
    public void ExportTargets() {
        using StreamWriter sw = File.AppendText("targets.csv");
        foreach (var target in Targets) {
            var line = target.Item1 + "," + target.Item2 + "," + target.Item3;
            sw.WriteLine(line);
        }
    }

    /// --------------------------------------
    /// Group of overriden expression visitors
    /// --------------------------------------
    protected override void VisitExpression(BinaryExpr bExpr) {
        if (!_replacementList.TryGetValue(bExpr.Op, out var replacementList)) 
            return;
        foreach (var replacement in replacementList) {
            // binary operator replacement
            Targets.Add((bExpr.Center.pos, "BOR", replacement.ToString()));
        }

        List<BinaryExpr.Opcode> relationalOperators = [BinaryExpr.Opcode.Eq, BinaryExpr.Opcode.Neq, 
            BinaryExpr.Opcode.Lt, BinaryExpr.Opcode.Le, BinaryExpr.Opcode.Gt, BinaryExpr.Opcode.Ge, 
            BinaryExpr.Opcode.Iff, BinaryExpr.Opcode.Imp, BinaryExpr.Opcode.Exp
        ];
        List<BinaryExpr.Opcode> conditionalOperators = [BinaryExpr.Opcode.And, BinaryExpr.Opcode.Or];
        if (relationalOperators.Contains(bExpr.Op) || conditionalOperators.Contains(bExpr.Op)) {
            // relational and conditional expressions can be replaced with true/false 
            // binary operator boolean replacement
            Targets.Add((bExpr.Center.pos, "BBR", "true"));
            Targets.Add((bExpr.Center.pos, "BBR", "false"));
        }
        
        base.VisitExpression(bExpr);
    }
}