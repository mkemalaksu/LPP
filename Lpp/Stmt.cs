public abstract record Stmt;

public record VarStmt(string Name, Expr Initializer) : Stmt;

public record ExpressionStmt(Expr Expression) : Stmt;

public record BlockStmt(List<Stmt> Statements) : Stmt;

public record IfStmt(Expr Condition, Stmt ThenBranch, Stmt? ElseBranch) : Stmt;

public record WhileStmt(Expr Condition, Stmt Body) : Stmt;

public record FunctionStmt(string Name, List<string> Parameters, List<Stmt> Body) : Stmt;

public record ReturnStmt(Token Keyword, Expr? Value) : Stmt;
