using System.Linq.Expressions;

namespace Lpp;

public class Parser
{
    private readonly List<Token> tokens;
    private int current = 0;
    
    public Parser(List<Token> tokens)
    {
        this.tokens = tokens;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd()) return false;
        return Peek().Type ==  type;
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

    private Token Consume(TokenType type, string message)
    {
        if (Check(type)) return Advance();
        throw new Exception($"[Line {Peek().Line}] Error: {message}");
    }
    private Token Peek() => tokens[current];
    private Token Previous() => tokens[current - 1];
    private bool IsAtEnd() => Peek().Type == TokenType.EOF;

    private Token Advance()
    {
        if (!IsAtEnd()) current++;
        return Previous();
    }

    private Expr Factor()
    {
        Expr expr = Unary();

        while (Match(TokenType.Star, TokenType.Slash, TokenType.Modulo))
        {
            Token op = Previous();
            Expr right = Unary();
            expr = new BinaryExpr(expr, op, right);
        }
        
        return expr;
    }

    private Expr Unary()
    {
        if (Match(TokenType.Bang, TokenType.Minus))
        {
            Token op = Previous();
            Expr right = Unary();
            return new UnaryExpr(op, right);
        }

        return Call();
    }

    private Expr Term()
    {
        Expr expr = Factor();

        while (Match(TokenType.Plus, TokenType.Minus))
        {
            Token op = Previous();
            Expr right = Factor();
            expr = new BinaryExpr(expr, op, right);
        }

        return expr;
    }

    private Stmt VarDeclaration()
    {
        Token name = Consume(TokenType.Identifier, "Expected variable name.");
        Consume(TokenType.Assign, "Expect '=' after variable name.");
        Expr initializer = Expression();
        
        Consume(TokenType.Semicolon, "Expected ';' after variable declaration.");

        return new VarStmt(name.Lexeme, initializer);
    }

    private Stmt Statement()
    {
        if (Match(TokenType.Function))
        {
            return FunctionDeclaration();
        }

        if (Match(TokenType.Return))
        {
            return ReturnStatement();
        }

        if (Match(TokenType.While))
        {
            return WhileStatement();
        }
        if (Match(TokenType.If))
        {
            return IfStatement();
        }

        if (Match(TokenType.LeftBrace))
        {
            return new BlockStmt(Block());
        }
        
        if (Match(TokenType.Local))
        {
            return VarDeclaration();
        }

        Expr expr = Expression();
        Consume(TokenType.Semicolon, "Expected ';' after statement.");

        return new ExpressionStmt(expr);
    }

    private Stmt FunctionDeclaration()
    {
        Token name = Consume(TokenType.Identifier, "Expected function name.");
        Consume(TokenType.LeftParenthesis, "Expected '(' after function name.");
        List<string> parameters = new();
        if (!Check(TokenType.RightParenthesis))
        {
            do
            {
                parameters.Add(Consume(TokenType.Identifier, "Expected parameter name.").Lexeme);
            } while (Match(TokenType.Comma));
        }
        Consume(TokenType.RightParenthesis, "Expected ')' after parameters.");
        Consume(TokenType.LeftBrace, "Expected '{' before function body.");
        List<Stmt> body = Block();
        return new FunctionStmt(name.Lexeme, parameters, body);
    }

    private Stmt ReturnStatement()
    {
        Token keyword = Previous();
        Expr? value = null;
        if (!Check(TokenType.Semicolon))
        {
            value = Expression();
        }
        Consume(TokenType.Semicolon, "Expected ';' after return value.");
        return new ReturnStmt(keyword, value);
    }

    private Stmt WhileStatement()
    {
        Expr condition = Expression();
        
        Consume(TokenType.LeftBrace, "Expected '{' after condition.");
        Stmt body = new BlockStmt(Block());
        
        return new WhileStmt(condition, body);
    }
    private Stmt IfStatement()
    {
        Expr condition = Expression();
        
        Consume(TokenType.LeftBrace, "Expected '{' after if condition.");
        Stmt thenBranch = new BlockStmt(Block());
        
        Stmt? elseBranch = null;
        if (Match(TokenType.Else))
        {
            if (Match(TokenType.If))
            {
                elseBranch = IfStatement();
            }
            else
            {
                Consume(TokenType.LeftBrace, "Expected '{' after else.");
                elseBranch = new BlockStmt(Block());
            }
        }
        
        return new IfStmt(condition, thenBranch, elseBranch);
    }

    private List<Stmt> Block()
    {
        List<Stmt> statements = new();
        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            statements.Add(Statement());
        }
        
        Consume(TokenType.RightBrace, "Expected '}' after block.");
        return statements;
    }
    private Expr Expression()
    {
        return Assignment();
    }

    private Expr Assignment()
    {
        Expr expr = Equality();
        if (Match(TokenType.Assign))
        {
            Expr right = Assignment();
            if (expr is VariableExpr variableExpr)
            {
                return new AssignExpr(variableExpr.Name, right);
            }
            else if (expr is IndexExpr indexExpr)
            {
                return new IndexAssignExpr(indexExpr.Target, indexExpr.Index, right);
            }
            else
            {
                throw new Exception("Assignment target must be a variable or array element.");
            }
        }
        else
        {
            return expr;
        }
    }
    
    private Expr Equality()
    {
        Expr expr = Comparison();

        while (Match(TokenType.EqualEqual, TokenType.BangEqual))
        {
            Token op = Previous();
            Expr right = Comparison();
            expr = new BinaryExpr(expr, op, right);
        }
        
        return expr;
    }

    private Expr Comparison()
    {
        Expr expr = Term();

        while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
        {
            Token op = Previous();
            Expr right = Term();
            expr = new BinaryExpr(expr, op, right);
        }

        return expr;
    }

    private Expr Primary()
    {
        if (Match(TokenType.LeftBracket))
        {
            List<Expr> elements = new();
            if (!Check(TokenType.RightBracket))
            {
                do
                {
                    elements.Add(Expression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightBracket, "Expected ']' after array elements.");
            return new ArrayExpr(elements);
        }

        if (Match(TokenType.Number, TokenType.String))
        {
            return new LiteralExpr(Previous().Value);
        }

        if (Match(TokenType.Identifier))
        {
            return new VariableExpr(Previous().Lexeme);
        }

        if (Match(TokenType.LeftParenthesis))
        {
            Expr expr = Expression();
            Consume(TokenType.RightParenthesis, "Expect ')' after expression.");
            return expr;
        }
        
        throw new Exception($"[Line {Peek().Line}] Error: Unexpected token '{Peek().Lexeme}'.");
    }

    private Expr Call()
    {
        Expr expr = Primary();

        while (true)
        {
            if (Match(TokenType.LeftParenthesis))
            {
                expr = FinishCall(expr);
            }
            else if (Match(TokenType.LeftBracket))
            {
                Expr index = Expression();
                Consume(TokenType.RightBracket, "Expected ']' after index.");
                expr = new IndexExpr(expr, index);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expr FinishCall(Expr callee)
    {
        List<Expr> arguments = new();

        if (!Check(TokenType.RightParenthesis))
        {
            do
            {
                arguments.Add(Expression());
            } while (Match(TokenType.Comma));
        }
        
        Consume(TokenType.RightParenthesis, "Expected ')' after arguments.");
        return new CallExpr(callee, arguments);

    }
    
    public List<Stmt> Parse()
    {
        List<Stmt> statements = new();
        while (!IsAtEnd())
        {
            statements.Add(Statement());
        }
        return statements;
    }
}