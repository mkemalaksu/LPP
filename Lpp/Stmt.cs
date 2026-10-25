public abstract record Stmt;

public record VarStmt(string Name, Expr Initializer) : Stmt;

public record ExpressionStmt(Expr Expression) : Stmt;

public record BlockStmt(List<Stmt> Statements) : Stmt;

public record IfStmt(Expr Condition, Stmt ThenBranch, Stmt? ElseBranch) : Stmt;

public record WhileStmt(Expr Condition, Stmt Body) : Stmt;

public record FunctionStmt(string Name, List<string> Parameters, List<Stmt> Body) : Stmt;

public record ReturnStmt(Token Keyword, Expr? Value) : Stmt;

public record BreakStmt(Token Keyword) : Stmt;

public record ContinueStmt(Token Keyword) : Stmt;

public record ForStmt(Stmt? Initializer, Expr? Condition, Expr? Increment, Stmt Body) : Stmt;

public record TryCatchStmt(Stmt TryBlock, string CatchVar, Stmt CatchBlock) : Stmt;

public record ForInStmt(string Var1, string? Var2, Expr Iterable, Stmt Body) : Stmt;

public record ClassStmt(string Name, List<FunctionStmt> Methods) : Stmt;
