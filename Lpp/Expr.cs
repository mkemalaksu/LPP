public abstract record Expr;

public record LiteralExpr(object? Value) : Expr;

public record VariableExpr(string Name) : Expr;

public record BinaryExpr(Expr Left, Token Operator, Expr Right) : Expr;

public record LogicalExpr(Expr Left, Token Operator, Expr Right) : Expr;

public record UnaryExpr(Token Operator, Expr Right) : Expr;

public record CallExpr(Expr Callee, List<Expr> Arguments) : Expr;

public record AssignExpr(string Name, Expr Value) : Expr;

public record ArrayExpr(List<Expr> Elements) : Expr;

public record IndexExpr(Expr Target, Expr Index) : Expr;

public record IndexAssignExpr(Expr Target, Expr Index, Expr Value) : Expr;

public record DictionaryExpr(List<(Expr Key, Expr Value)> Pairs) : Expr;

public record DotExpr(Expr Target, string Property) : Expr;

public record DotAssignExpr(Expr Target, string Property, Expr Value) : Expr;

public record InterpolatedStringExpr(List<Expr> Parts) : Expr;

public record TernaryExpr(Expr Condition, Expr ThenBranch, Expr ElseBranch) : Expr;

public record AnonymousFunctionExpr(List<string> Parameters, List<Stmt> Body) : Expr;

public record NewExpr(string ClassName, List<Expr> Arguments) : Expr;

public record ThisExpr(Token Keyword) : Expr;
