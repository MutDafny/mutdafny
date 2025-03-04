using Microsoft.Dafny;

namespace MutDafny.TargetFinder;

// this type of finder is used to find the statement in which specific binary operators are used
public class BinaryOpTargetFinder(int mutationTargetPos, ErrorReporter reporter) 
    : TargetFinder(mutationTargetPos, reporter)
{
    protected override Statement? HandleStatement(Statement statement)
    {
        Statement? statementToReturn = statement switch {
            AssignStatement aStmt => aStmt,
            VarDeclStmt vDeclStmt => vDeclStmt.Assign,
            ReturnStmt rStmt => rStmt,
            IfStmt ifStmt => HandleIfStatement(ifStmt),
            WhileStmt whlStmt => HandleWhileStatement(whlStmt), 
            ForLoopStmt forStmt => HandleBlock(forStmt.Body),
            // TODO: check different types of expressions
            _ => null
        }; 
        return statementToReturn;
    }

    private Statement? HandleIfStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && 
            IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            return ifStmt;
        } else if (IsWorthVisiting(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            return HandleBlock(ifStmt.Thn);
        } else if (ifStmt.Els is BlockStmt bEls) {
            return HandleBlock(bEls);
        } else {
            return ifStmt.Els;
        }
    }

    private Statement? HandleWhileStatement(WhileStmt whileStmt)
    {
        return IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos) ? 
            whileStmt : HandleBlock(whileStmt.Body);
    }
}