using Microsoft.Dafny;

namespace MutDafny.TargetFinder;

// this type of finder is used to find the statement in which specific binary operators are used
public class BinaryOpTargetFinder(int mutationTargetPos, ErrorReporter reporter) 
    : TargetFinder(mutationTargetPos, reporter)
{
    protected override Statement? HandleStatement(Statement statement)
    {
        switch (statement) {
            case AssignStatement aStmt:
                HandleRhsList(aStmt.Rhss); return aStmt;
            case VarDeclStmt vDeclStmt:
                return vDeclStmt.Assign != null ? 
                    HandleStatement(vDeclStmt.Assign) : 
                    null;
            case ReturnStmt rStmt:
                HandleRhsList(rStmt.Rhss); return rStmt;
            case IfStmt ifStmt:
                return HandleIfStatement(ifStmt);
            case WhileStmt whlStmt:
                return HandleWhileStatement(whlStmt);
            case ForLoopStmt forStmt:
                return HandleForLoopStatement(forStmt);
            // TODO: check different types of statements
        }
        return null;
    }
    
    private Statement? HandleIfStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && 
            IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            HandleExpression(ifStmt.Guard);
            return ifStmt;
        } if (IsWorthVisiting(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            return HandleBlock(ifStmt.Thn);
        } if (ifStmt.Els is BlockStmt bEls) {
            return HandleBlock(bEls);
        }
        return ifStmt.Els;
    }
    
    private Statement? HandleWhileStatement(WhileStmt whileStmt)
    {
        if (IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos)) {
            HandleExpression(whileStmt.Guard);
            return whileStmt;
        }
        return HandleBlock(whileStmt.Body);
    }

    private Statement? HandleForLoopStatement(ForLoopStmt forStmt)
    {
        if (IsWorthVisiting(forStmt.Start.StartToken.pos, forStmt.Start.EndToken.pos)) {
            HandleExpression(forStmt.Start);
            return forStmt;
        } if (IsWorthVisiting(forStmt.End.StartToken.pos, forStmt.End.EndToken.pos)) {
            HandleExpression(forStmt.End);
            return forStmt;
        }
        return HandleBlock(forStmt.Body);
    }

    private void HandleRhsList(List<AssignmentRhs> rhss)
    {
        foreach (var rhs in rhss) {
            if (rhs is not ExprRhs exprRhs) continue;
            if (IsWorthVisiting(rhs.StartToken.pos, rhs.EndToken.pos)) {
                HandleExpression(exprRhs.Expr);
            }
        }
    }

    private void HandleExpression(Expression expr)
    {
        switch (expr) {
            case BinaryExpr bExpr:
                if (IsTarget(bExpr)) {
                    TargetExpression = bExpr;
                    break;
                }
                HandleExpression(bExpr.E0);
                if (TargetExpression != null) break;
                HandleExpression(bExpr.E1); break;
            case ParensExpression pExpr:
                HandleExpression(pExpr.E); break;
            // TODO: check different types of expressions
        }
    }
    
    private bool IsTarget(BinaryExpr expr)
    {
        return expr.Center.pos == mutationTargetPos;
    }
}