using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lpp;

public class LuaTranspiler
{
    private int indentLevel = 0;

    private string Indent() => new string(' ', indentLevel * 4);

    public string Transpile(List<Stmt> statements)
    {
        var sb = new StringBuilder();
        foreach (var stmt in statements)
        {
            sb.AppendLine(TranspileStmt(stmt));
        }
        return sb.ToString();
    }

    private string TranspileStmt(Stmt stmt)
    {
        return stmt switch
        {
            VarStmt v => $"{Indent()}local {v.Name} = {(v.Initializer != null ? TranspileExpr(v.Initializer) : "nil")}",
            ExpressionStmt e => $"{Indent()}{TranspileExpr(e.Expression)}",
            BlockStmt b => TranspileBlock(b),
            IfStmt i => TranspileIf(i),
            WhileStmt w => TranspileWhile(w),
            _ => throw new Exception($"Transpiler Error: Unknown statement type {stmt.GetType()}")
        };
    }

    private string TranspileBlock(BlockStmt block)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent()}do");
        indentLevel++;
        foreach (var s in block.Statements)
        {
            sb.AppendLine(TranspileStmt(s));
        }
        indentLevel--;
        sb.Append($"{Indent()}end");
        return sb.ToString();
    }

    private string TranspileIf(IfStmt ifStmt)
    {
        var sb = new StringBuilder();
        sb.Append($"{Indent()}if {TranspileExpr(ifStmt.Condition)} then\n");
        
        indentLevel++;
        if (ifStmt.ThenBranch is BlockStmt thenBlock)
        {
            foreach (var s in thenBlock.Statements) sb.AppendLine(TranspileStmt(s));
        }
        else
        {
            sb.AppendLine(TranspileStmt(ifStmt.ThenBranch));
        }
        indentLevel--;

        if (ifStmt.ElseBranch != null)
        {
            sb.AppendLine($"{Indent()}else");
            indentLevel++;
            if (ifStmt.ElseBranch is BlockStmt elseBlock)
            {
                foreach (var s in elseBlock.Statements) sb.AppendLine(TranspileStmt(s));
            }
            else
            {
                sb.AppendLine(TranspileStmt(ifStmt.ElseBranch));
            }
            indentLevel--;
        }

        sb.Append($"{Indent()}end");
        return sb.ToString();
    }

    private string TranspileWhile(WhileStmt whileStmt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Indent()}while {TranspileExpr(whileStmt.Condition)} do");
        indentLevel++;
        if (whileStmt.Body is BlockStmt bodyBlock)
        {
            foreach (var s in bodyBlock.Statements) sb.AppendLine(TranspileStmt(s));
        }
        else
        {
            sb.AppendLine(TranspileStmt(whileStmt.Body));
        }
        indentLevel--;
        sb.Append($"{Indent()}end");
        return sb.ToString();
    }

    private string TranspileExpr(Expr expr)
    {
        return expr switch
        {
            LiteralExpr l => l.Value is string ? $"\"{l.Value}\"" : l.Value?.ToString()?.ToLower() ?? "nil",
            VariableExpr v => v.Name,
            BinaryExpr b => $"{TranspileExpr(b.Left)} {MapOperator(b.Operator.Type)} {TranspileExpr(b.Right)}",
            AssignExpr a => $"{a.Name} = {TranspileExpr(a.Value)}",
            CallExpr c => $"{TranspileExpr(c.Callee)}({string.Join(", ", c.Arguments.Select(TranspileExpr))})",
            _ => throw new Exception($"Transpiler Error: Unknown expression type {expr.GetType()}")
        };
    }

    private string MapOperator(TokenType type)
    {
        return type switch
        {
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Slash => "/",
            TokenType.Modulo => "%",
            TokenType.Greater => ">",
            TokenType.GreaterEqual => ">=",
            TokenType.Less => "<",
            TokenType.LessEqual => "<=",
            TokenType.EqualEqual => "==",
            TokenType.BangEqual => "~=", // Lua dilinde eşit değildir işareti ~= şeklindedir!
            _ => throw new Exception($"Transpiler Error: Unknown operator {type}")
        };
    }
}
