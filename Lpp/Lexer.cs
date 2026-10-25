namespace Lpp;
using System.Globalization;

public class Lexer
{
    private string source;
    private List<Token> tokens = new();
    private int start = 0;
    private int position = 0;
    private int line = 1;
    private int column = 1;

    public Lexer(string source)
    {
        this.source = source;
    }

    public List<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            start = position;
            ScanToken();
        }

        tokens.Add(new Token(TokenType.EOF, "", null, line, column));
        return tokens;
    }

    void ScanToken()
    {
        char c = Advance();
        switch (c)
        {
            case '+':
                AddToken(TokenType.Plus);
                break;
            case ';':
                AddToken(TokenType.Semicolon);
                break;
            case '-':
                AddToken(TokenType.Minus);
                break;
            case '*':
                AddToken(TokenType.Star);
                break;
            case '/':
                if (Match('/'))
                {
                    while (Peek() != '\n' && !IsAtEnd()) Advance();
                    break;
                }
                AddToken(TokenType.Slash);
                break;
            case '=':
                AddToken(Match('=') ? TokenType.EqualEqual : TokenType.Assign);
                break;
            case '{': AddToken(TokenType.LeftBrace); break;
            case '}': AddToken(TokenType.RightBrace); break;
            case '[': AddToken(TokenType.LeftBracket); break;
            case ']': AddToken(TokenType.RightBracket); break;
            case '!':
                AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang);
                break;
            case '<':
                AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less);
                break;
            case '>':
                AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater);
                break;

            case ',':
                AddToken(TokenType.Comma);
                break;
            case '"':
                ScanString();
                break;
            case '(':
                AddToken(TokenType.LeftParenthesis);
                break;
            case ')':
                AddToken(TokenType.RightParenthesis);
                break;
            case '%':
                AddToken(TokenType.Modulo);
                break;
            case '\n':
                line++;
                column = 1;
                break;
            case ' ' or '\t' or '\r':
                break;
            default:
                if (char.IsDigit(c))
                {
                    Number();
                    break;
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    Identifier();
                    break;
                }

                Console.WriteLine("Unexpected token: " + c);
                break;
        }
    }

    bool Match(char expected)
    {
        if (position  >= source.Length) return false;
        
        if (source[position] != expected) return false;

        Advance();
        return true;
    }
    void ScanString()
    {
        while (Peek() != '"' && !IsAtEnd())
        {
            Advance();
        }

        if (IsAtEnd())
        {
            throw new Exception($"[Line {line}] Unterminated string.");
        }

        Advance();

        string value = source.Substring(start + 1, position - start - 2);
        AddToken(TokenType.String, value);
    }

    void Number()
    {
        while (char.IsDigit(Peek())) Advance();

        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            Advance();

            while (char.IsDigit(Peek())) Advance();
        }

        string text = source.Substring(start, position - start);
        double number = double.Parse(text, CultureInfo.InvariantCulture);
        AddToken(TokenType.Number, number);
    }


    void Identifier()
    {
        while (char.IsLetterOrDigit(Peek()) || Peek() == '_') Advance();
        string identifier = source.Substring(start, position - start);
        switch (identifier)
        {
            case "local": AddToken(TokenType.Local); break;
            case "if": AddToken(TokenType.If); break;
            case "else": AddToken(TokenType.Else); break;
            case "while": AddToken(TokenType.While); break;
            case "function": AddToken(TokenType.Function); break;
            case "return": AddToken(TokenType.Return); break;

            default: AddToken(TokenType.Identifier); break;
        }
    }

    void AddToken(TokenType type, object? value = null)
    {
        string lexeme = source.Substring(start, position - start);
        tokens.Add(new Token(type, lexeme, value, line, column));
    }

    char Advance()
    {
        position++;
        column++;
        return source[position - 1];
    }

    char Peek()
    {
        if (IsAtEnd()) return '\0';
        return source[position];
    }
    char PeekNext()
    {
        if (position + 1 >= source.Length) return '\0';
        return source[position + 1];
    }
    bool IsAtEnd()
    {
        return position >= source.Length;
    }
}
