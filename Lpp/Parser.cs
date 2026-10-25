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
        if (Match(TokenType.Class))
        {
            return ClassDeclaration();
        }

        if (Match(TokenType.Function))
        {
            return FunctionDeclaration();
        }

        if (Match(TokenType.Return))
        {
            return ReturnStatement();
        }

        if (Match(TokenType.Try))
        {
            return TryCatchStatement();
        }

        if (Match(TokenType.Break))
        {
            Token keyword = Previous();
            Consume(TokenType.Semicolon, "Expected ';' after 'break'.");
            return new BreakStmt(keyword);
        }

        if (Match(TokenType.Continue))
        {
            Token keyword = Previous();
            Consume(TokenType.Semicolon, "Expected ';' after 'continue'.");
            return new ContinueStmt(keyword);
        }

        if (Match(TokenType.For))
        {
            return ForStatement();
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

    private Stmt ClassDeclaration()
    {
        Token name = Consume(TokenType.Identifier, "Expected class name.");
        Consume(TokenType.LeftBrace, "Expected '{' before class body.");

        List<FunctionStmt> methods = new();
        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            Match(TokenType.Function); // 'function' keyword is optional
            methods.Add((FunctionStmt)FunctionDeclaration());
        }

        Consume(TokenType.RightBrace, "Expected '}' after class body.");
        return new ClassStmt(name.Lexeme, methods);
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

    private Stmt TryCatchStatement()
    {
        Stmt tryBlock = Statement();
        Consume(TokenType.Catch, "Expected 'catch' after 'try' block.");

        string catchVar = "err";
        if (Match(TokenType.LeftParenthesis))
        {
            catchVar = Consume(TokenType.Identifier, "Expected catch variable name.").Lexeme;
            Consume(TokenType.RightParenthesis, "Expected ')' after catch variable.");
        }

        Stmt catchBlock = Statement();
        return new TryCatchStmt(tryBlock, catchVar, catchBlock);
    }

    private Stmt ForStatement()
    {
        bool hasParen = Match(TokenType.LeftParenthesis);

        // Check for for-in: for (item in list) or for (k, v in dict) or without parentheses
        int checkpoint = current;
        Match(TokenType.Local); // optional local

        if (Check(TokenType.Identifier))
        {
            Token var1 = Advance();
            string? var2 = null;
            if (Match(TokenType.Comma))
            {
                var2 = Consume(TokenType.Identifier, "Expected second variable name in for-in.").Lexeme;
            }

            if (Match(TokenType.In))
            {
                Expr iterable = Expression();
                if (hasParen) Consume(TokenType.RightParenthesis, "Expected ')' after for-in.");
                Stmt forInBody = Statement();
                return new ForInStmt(var1.Lexeme, var2, iterable, forInBody);
            }

            current = checkpoint; // Not a for-in loop, rewind
        }
        else
        {
            current = checkpoint;
        }

        // Classic for: for (initializer; condition; increment)
        if (!hasParen) Consume(TokenType.LeftParenthesis, "Expect '(' after 'for'.");

        Stmt? initializer;
        if (Match(TokenType.Semicolon))
        {
            initializer = null;
        }
        else if (Match(TokenType.Local))
        {
            initializer = VarDeclaration();
        }
        else
        {
            Expr expr = Expression();
            Consume(TokenType.Semicolon, "Expect ';' after loop initializer.");
            initializer = new ExpressionStmt(expr);
        }

        Expr? condition = null;
        if (!Check(TokenType.Semicolon))
        {
            condition = Expression();
        }
        Consume(TokenType.Semicolon, "Expect ';' after loop condition.");

        Expr? increment = null;
        if (!Check(TokenType.RightParenthesis))
        {
            increment = Expression();
        }
        Consume(TokenType.RightParenthesis, "Expect ')' after for clauses.");

        Stmt body = Statement();

        return new ForStmt(initializer, condition, increment, body);
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
        Expr expr = Ternary();
        if (Match(TokenType.Assign, TokenType.PlusEqual, TokenType.MinusEqual, TokenType.StarEqual, TokenType.SlashEqual))
        {
            Token op = Previous();
            Expr right = Assignment();

            if (op.Type != TokenType.Assign)
            {
                TokenType binType = op.Type switch
                {
                    TokenType.PlusEqual => TokenType.Plus,
                    TokenType.MinusEqual => TokenType.Minus,
                    TokenType.StarEqual => TokenType.Star,
                    TokenType.SlashEqual => TokenType.Slash,
                    _ => TokenType.Plus
                };
                right = new BinaryExpr(expr, new Token(binType, op.Lexeme, null, op.Line, op.Column), right);
            }

            if (expr is VariableExpr variableExpr)
            {
                return new AssignExpr(variableExpr.Name, right);
            }
            else if (expr is IndexExpr indexExpr)
            {
                return new IndexAssignExpr(indexExpr.Target, indexExpr.Index, right);
            }
            else if (expr is DotExpr dotExpr)
            {
                return new DotAssignExpr(dotExpr.Target, dotExpr.Property, right);
            }
            else
            {
                throw new Exception("Assignment target must be a variable, array element or property.");
            }
        }
        else
        {
            return expr;
        }
    }

    private Expr Ternary()
    {
        Expr expr = LogicalOr();

        if (Match(TokenType.Question))
        {
            Expr thenBranch = Expression();
            Consume(TokenType.Colon, "Expected ':' in ternary expression.");
            Expr elseBranch = Ternary();
            return new TernaryExpr(expr, thenBranch, elseBranch);
        }

        return expr;
    }

    private Expr LogicalOr()
    {
        Expr expr = LogicalAnd();

        while (Match(TokenType.Or))
        {
            Token op = Previous();
            Expr right = LogicalAnd();
            expr = new LogicalExpr(expr, op, right);
        }

        return expr;
    }

    private Expr LogicalAnd()
    {
        Expr expr = Equality();

        while (Match(TokenType.And))
        {
            Token op = Previous();
            Expr right = Equality();
            expr = new LogicalExpr(expr, op, right);
        }

        return expr;
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

    private Expr ParseInterpolatedString(string raw)
    {
        List<Expr> parts = new();
        int i = 0;
        while (i < raw.Length)
        {
            int braceStart = raw.IndexOf('{', i);
            if (braceStart == -1)
            {
                parts.Add(new LiteralExpr(raw.Substring(i)));
                break;
            }
            if (braceStart > i)
            {
                parts.Add(new LiteralExpr(raw.Substring(i, braceStart - i)));
            }
            int braceEnd = raw.IndexOf('}', braceStart);
            if (braceEnd == -1)
            {
                throw new Exception("Unclosed '{' in interpolated string.");
            }
            string exprCode = raw.Substring(braceStart + 1, braceEnd - braceStart - 1);
            var subLexer = new Lexer(exprCode);
            var subTokens = subLexer.Tokenize();
            var subParser = new Parser(subTokens);
            parts.Add(subParser.Expression());
            i = braceEnd + 1;
        }
        return new InterpolatedStringExpr(parts);
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
        if (Match(TokenType.LeftBrace))
        {
            List<(Expr Key, Expr Value)> pairs = new();
            if (!Check(TokenType.RightBrace))
            {
                do
                {
                    Expr key = Expression();
                    Consume(TokenType.Colon, "Expected ':' after dictionary key.");
                    Expr value = Expression();
                    pairs.Add((key, value));
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightBrace, "Expected '}' after dictionary.");
            return new DictionaryExpr(pairs);
        }

        if (Match(TokenType.InterpolatedString))
        {
            string raw = (string)Previous().Value!;
            return ParseInterpolatedString(raw);
        }

        if (Match(TokenType.New))
        {
            Token className = Consume(TokenType.Identifier, "Expected class name after 'new'.");
            Consume(TokenType.LeftParenthesis, "Expected '(' after class name.");
            List<Expr> arguments = new();
            if (!Check(TokenType.RightParenthesis))
            {
                do
                {
                    arguments.Add(Expression());
                } while (Match(TokenType.Comma));
            }
            Consume(TokenType.RightParenthesis, "Expected ')' after arguments.");
            return new NewExpr(className.Lexeme, arguments);
        }

        if (Match(TokenType.This))
        {
            return new ThisExpr(Previous());
        }

        if (Match(TokenType.Function))
        {
            Consume(TokenType.LeftParenthesis, "Expected '(' after 'function'.");
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
            return new AnonymousFunctionExpr(parameters, body);
        }

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
            else if (Match(TokenType.Dot))
            {
                Token prop = Consume(TokenType.Identifier, "Expected property name after '.'.");
                expr = new DotExpr(expr, prop.Lexeme);
            }
            else if (Match(TokenType.PlusPlus, TokenType.MinusMinus))
            {
                Token op = Previous();
                TokenType binType = op.Type == TokenType.PlusPlus ? TokenType.Plus : TokenType.Minus;
                Expr one = new LiteralExpr(1.0);
                Expr newVal = new BinaryExpr(expr, new Token(binType, op.Lexeme, null, op.Line, op.Column), one);

                if (expr is VariableExpr v) expr = new AssignExpr(v.Name, newVal);
                else if (expr is IndexExpr idx) expr = new IndexAssignExpr(idx.Target, idx.Index, newVal);
                else if (expr is DotExpr dot) expr = new DotAssignExpr(dot.Target, dot.Property, newVal);
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