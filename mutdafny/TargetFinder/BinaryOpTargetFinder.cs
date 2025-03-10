using Microsoft.Dafny;

namespace MutDafny.TargetFinder;

// this type of finder is used to find the statement in which specific binary operators are used
public class BinaryOpTargetFinder(int mutationTargetPos, ErrorReporter reporter) 
    : TargetFinder(mutationTargetPos, reporter)
{
    protected override void HandleStatement(Statement statement)
    {
        switch (statement) {
            case AssignStatement aStmt:
                HandleRhsList(aStmt.Rhss); break;
            case VarDeclStmt vDeclStmt:
                if (vDeclStmt.Assign != null) 
                    HandleStatement(vDeclStmt.Assign);
                break;
            case ReturnStmt rStmt:
                HandleRhsList(rStmt.Rhss); break;
            case IfStmt ifStmt:
                HandleIfStatement(ifStmt); break;
            case WhileStmt whlStmt:
                HandleWhileStatement(whlStmt); break;
            case ForLoopStmt forStmt:
                HandleForLoopStatement(forStmt); break;
            // TODO: check different types of statements
        }
    }
    
    private void HandleIfStatement(IfStmt ifStmt) {
        if (ifStmt.Guard != null && 
            IsWorthVisiting(ifStmt.Guard.StartToken.pos, ifStmt.Guard.EndToken.pos)) {
            HandleExpression(ifStmt.Guard);
        } if (IsWorthVisiting(ifStmt.Thn.StartToken.pos, ifStmt.Thn.EndToken.pos)) {
            HandleBlock(ifStmt.Thn);
        } if (ifStmt.Els is BlockStmt bEls) {
            HandleBlock(bEls);
        } if (ifStmt.Els != null) {
            HandleStatement(ifStmt.Els);
        }
    }
    
    private void HandleWhileStatement(WhileStmt whileStmt)
    {
        if (IsWorthVisiting(whileStmt.Guard.StartToken.pos, whileStmt.Guard.EndToken.pos)) {
            HandleExpression(whileStmt.Guard);
        }
        HandleBlock(whileStmt.Body);
    }

    private void HandleForLoopStatement(ForLoopStmt forStmt)
    {
        if (IsWorthVisiting(forStmt.Start.StartToken.pos, forStmt.Start.EndToken.pos)) {
            HandleExpression(forStmt.Start);
        } if (IsWorthVisiting(forStmt.End.StartToken.pos, forStmt.End.EndToken.pos)) {
            HandleExpression(forStmt.End);
        }
        HandleBlock(forStmt.Body);
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