public abstract record Expr;

public record LiteralExpr(object? Value) : Expr;

public record VariableExpr(string Name) : Expr;

public record BinaryExpr(Expr Left, Token Operator, Expr Right) : Expr;

public record UnaryExpr(Token Operator, Expr Right) : Expr;

public record CallExpr(Expr Callee, List<Expr> Arguments) : Expr;

public record AssignExpr(string Name, Expr Value) : Expr;

public record ArrayExpr(List<Expr> Elements) : Expr;

public record IndexExpr(Expr Target, Expr Index) : Expr;

public record IndexAssignExpr(Expr Target, Expr Index, Expr Value) : Expr;
