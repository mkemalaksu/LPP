public record Token(TokenType Type,
    string Lexeme,
    object? Value,
    int Line, int Column);