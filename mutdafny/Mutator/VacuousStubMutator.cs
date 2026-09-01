using System.Numerics;
using Microsoft.Dafny;

namespace MutDafny.Mutator;

public class VacuousStubMutator(string mutationTargetPos, string stubPlan, ErrorReporter reporter)
    : Mutator(mutationTargetPos, reporter)
{
    private const string EmptyBodyPlan = "empty";
    private readonly List<string> _outValues = stubPlan.Split('+').ToList();

    private bool IsTarget(Method method) {
        var positions = MutationTargetPos.Split("-");
        if (positions.Length < 2) return false;
        if (!int.TryParse(positions[0], out var startPosition) ||
            !int.TryParse(positions[1], out var endPosition))
            return false;

        return method.StartToken.pos == startPosition &&
               method.EndToken.pos == endPosition &&
               !AlreadyMutated(method) && !ContainsMutatedChildren(method);
    }

    private Expression? CreateStubValue(string token, IOrigin origin) {
        if (token.Length < 2) return null;
        var payload = token[1..];

        return token[0] switch {
            'd' => CreateDefaultExpression(payload, origin),
            'c' => CreateDefaultExpression($"datatype:{payload}", origin),
            'v' => new NameSegment(origin, payload, null),
            'l' => BigInteger.TryParse(payload, out var literal)
                ? new LiteralExpr(origin, literal)
                : null,
            _ => null,
        };
    }

    /// ---------------------------
    /// Group of overriden visitors
    /// ---------------------------
    protected override void HandleMethod(Method method) {
        if (method.Body == null || method.Body is DividedBlockStmt || !IsTarget(method)) {
            base.HandleMethod(method);
            return;
        }
        var origin = method.Body.Origin;
        var stub = new List<Statement>();
        if (stubPlan == EmptyBodyPlan) {
            if (method.Outs.Count != 0) return;
        } else {
            if (method.Outs.Count != _outValues.Count) return;
            for (var i = 0; i < method.Outs.Count; i++) {
                var value = CreateStubValue(_outValues[i], origin);
                if (value == null) return;
                var outParam = new NameSegment(origin, method.Outs[i].Name, null);
                stub.Add(new AssignStatement(origin, [outParam], [new ExprRhs(value)]));
            }
        }

        MutantGenerator.NumMutations++;
        MutantGenerator.MutatedNodes.Add(method);
        method.Body = new BlockStmt(origin, stub);
        TargetStatement = method.Body;
    }
}
