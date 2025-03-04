using Microsoft.Dafny;

namespace MutDafny.Mutator;

public abstract class Mutator
{
    public abstract void Mutate(Statement statement);

    protected void ReplaceStmtInTargetBlock(Statement oldStmt, Statement newStmt)
    {
        var targetBlock = TargetFinder.TargetFinder.TargetBlock;
        if (targetBlock == null) return;
        var i = targetBlock.Body.IndexOf(oldStmt);
        if (i == -1) return;
        targetBlock.Body.RemoveAt(i);
        targetBlock.Body.Insert(i, newStmt);
    }
}