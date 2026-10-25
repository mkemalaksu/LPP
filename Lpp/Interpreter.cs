namespace Lpp;

public class Environment
{
    private readonly Environment? enclosing;
    private readonly Dictionary<string, object?> values = new();

    public Environment(Environment? enclosing = null)
    {
        this.enclosing = enclosing;
    }

    public void Define(string name, object? value)
    {
        values[name] = value;
    }

    public bool IsDefinedInCurrentScope(string name) => values.ContainsKey(name);

    public object? Get(string name)
    {
        if (values.TryGetValue(name, out var value))
        {
            return value;
        }

        if (enclosing != null)
        {
            return enclosing.Get(name);
        }

        throw new Exception($"Runtime Error: Undefined variable '{name}'.");
    }

    public void Assign(string name, object? value)
    {
        if (values.ContainsKey(name))
        {
            values[name] = value;
            return;
        }

        if (enclosing != null)
        {
            enclosing.Assign(name, value);
            return;
        }

        throw new Exception($"Runtime Error: Cannot assign to undefined variable '{name}'.");
    }
}

public class ReturnException : Exception
{
    public object? Value { get; }
    public ReturnException(object? value)
    {
        Value = value;
    }
}

public class LppFunction : ICallable
{
    private readonly FunctionStmt declaration;
    private readonly Environment closure;

    public LppFunction(FunctionStmt declaration, Environment closure)
    {
        this.declaration = declaration;
        this.closure = closure;
    }

    public int Arity => declaration.Parameters.Count;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        Environment env = new Environment(closure);
        for (int i = 0; i < declaration.Parameters.Count; i++)
        {
            env.Define(declaration.Parameters[i], arguments[i]);
        }

        try
        {
            interpreter.ExecuteBlock(declaration.Body, env);
        }
        catch (ReturnException returnException)
        {
            return returnException.Value;
        }

        return null;
    }
}

public class Interpreter
{
    private Environment environment = new();

    public Interpreter()
    {
        environment.Define("print", new BuiltinPrint());
        environment.Define("input", new BuiltinInput());
        environment.Define("random", new BuiltinRandom());
        environment.Define("clear", new BuiltinClear());
        environment.Define("sleep", new BuiltinSleep());
        environment.Define("len", new BuiltinLen());
        environment.Define("push", new BuiltinPush());
    }

    public void Interpret(List<Stmt> statements)
    {
        foreach (var stmt in statements)
        {
            Execute(stmt);
        }
    }

    public void ExecuteBlock(List<Stmt> statements, Environment newEnv)
    {
        Environment previous = environment;
        try
        {
            environment = newEnv;
            foreach (var statement in statements)
            {
                Execute(statement);
            }
        }
        finally
        {
            environment = previous;
        }
    }

    private void Execute(Stmt stmt)
    {
        switch (stmt)
        {
            case FunctionStmt fnStmt:
                var function = new LppFunction(fnStmt, environment);
                environment.Define(fnStmt.Name, function);
                break;

            case ReturnStmt returnStmt:
                object? retVal = null;
                if (returnStmt.Value != null)
                {
                    retVal = Evaluate(returnStmt.Value);
                }
                throw new ReturnException(retVal);

            case VarStmt varStmt:
                if (environment.IsDefinedInCurrentScope(varStmt.Name))
                {
                    throw new Exception($"Runtime Error: Variable '{varStmt.Name}' is already defined in this scope.");
                }

                object? value = null;
                if (varStmt.Initializer != null)
                {
                    value = Evaluate(varStmt.Initializer);
                }
                environment.Define(varStmt.Name, value);
                break;
                
            case ExpressionStmt exprStmt:
                Evaluate(exprStmt.Expression);
                break;

            case BlockStmt blockStmt:
                ExecuteBlock(blockStmt.Statements, new Environment(environment));
                break;

            case IfStmt ifStmt:
                if (IsTruthy(Evaluate(ifStmt.Condition)))
                {
                    Execute(ifStmt.ThenBranch);
                }
                else if (ifStmt.ElseBranch != null)
                {
                    Execute(ifStmt.ElseBranch);
                }
                break;

            case WhileStmt whileStmt:
                while (IsTruthy(Evaluate(whileStmt.Condition)))
                {
                    Execute(whileStmt.Body);
                }
                break;
            
            default:
                throw new Exception($"Runtime Error: Unknown statement type {stmt.GetType()}.");
        }
    }

    bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        return true;
    }
    
    private object? Evaluate(Expr expr)
    {
        return expr switch
        {
            LiteralExpr literal => literal.Value,
            VariableExpr variable => environment.Get(variable.Name),
            BinaryExpr binary => EvaluateBinary(binary),
            UnaryExpr unary => EvaluateUnary(unary),
            CallExpr call => EvaluateCall(call),
            AssignExpr assign => EvaluateAssign(assign),
            ArrayExpr arrayExpr => EvaluateArray(arrayExpr),
            IndexExpr indexExpr => EvaluateIndex(indexExpr),
            IndexAssignExpr indexAssign => EvaluateIndexAssign(indexAssign),
            _ => throw new Exception($"Unknown expression type {expr.GetType()}")
        };
    }

    private object? EvaluateUnary(UnaryExpr unary)
    {
        object? right = Evaluate(unary.Right);

        if (unary.Operator.Type == TokenType.Minus)
        {
            if (right is double num) return -num;
            throw new Exception($"[Line {unary.Operator.Line}] Runtime Error: Operand must be a number for unary '-'.");
        }

        if (unary.Operator.Type == TokenType.Bang)
        {
            return !IsTruthy(right);
        }

        throw new Exception($"[Line {unary.Operator.Line}] Runtime Error: Unknown unary operator '{unary.Operator.Lexeme}'.");
    }

    private object? EvaluateArray(ArrayExpr arrayExpr)
    {
        List<object?> elements = new();
        foreach (var element in arrayExpr.Elements)
        {
            elements.Add(Evaluate(element));
        }
        return elements;
    }

    private object? EvaluateIndex(IndexExpr indexExpr)
    {
        object? target = Evaluate(indexExpr.Target);
        object? indexVal = Evaluate(indexExpr.Index);

        if (indexVal is not double dIndex)
        {
            throw new Exception("Runtime Error: Array index must be a number.");
        }

        int index = (int)dIndex;

        if (target is List<object?> list)
        {
            if (index < 0 || index >= list.Count)
            {
                throw new Exception($"Runtime Error: Index {index} out of range (array length: {list.Count}).");
            }
            return list[index];
        }

        if (target is string str)
        {
            if (index < 0 || index >= str.Length)
            {
                throw new Exception($"Runtime Error: Index {index} out of range (string length: {str.Length}).");
            }
            return str[index].ToString();
        }

        throw new Exception("Runtime Error: Index operator can only be used on arrays and strings.");
    }

    private object? EvaluateIndexAssign(IndexAssignExpr indexAssign)
    {
        object? target = Evaluate(indexAssign.Target);
        object? indexVal = Evaluate(indexAssign.Index);
        object? val = Evaluate(indexAssign.Value);

        if (indexVal is not double dIndex)
        {
            throw new Exception("Runtime Error: Array index must be a number.");
        }

        int index = (int)dIndex;

        if (target is List<object?> list)
        {
            if (index < 0 || index >= list.Count)
            {
                throw new Exception($"Runtime Error: Index {index} out of range (array length: {list.Count}).");
            }
            list[index] = val;
            return val;
        }

        throw new Exception("Runtime Error: Can only assign to elements of an array.");
    }

    private object? EvaluateAssign(AssignExpr assign)
    {
        object? value = Evaluate(assign.Value);
        environment.Assign(assign.Name, value);
        return value;
    }

    private object? EvaluateCall(CallExpr call)
    {
        object? callee = Evaluate(call.Callee);

        List<object?> arguments = new();
        foreach (var arg in call.Arguments)
        {
            arguments.Add(Evaluate(arg));
        }

        if (callee is not ICallable function)
        {
            throw new Exception("Runtime Error: Can only call functions.");
        }
        
        if (function.Arity != -1 && arguments.Count != function.Arity)
        {
            throw new Exception($"Runtime Error: Expected {function.Arity} arguments but got {arguments.Count}.");
        }

        return function.Call(this, arguments);
    }
    
    public object? GetEnvironmentVariable(string name) => environment.Get(name);

    private object? EvaluateBinary(BinaryExpr binary)
    {
        object? left = Evaluate(binary.Left);
        object? right = Evaluate(binary.Right);

        if (binary.Operator.Type == TokenType.EqualEqual) return object.Equals(left, right);
        if (binary.Operator.Type == TokenType.BangEqual) return !object.Equals(left, right);

        if (binary.Operator.Type == TokenType.Plus)
        {
            if (left is string || right is string)
            {
                return LppPrinter.Stringify(left) + LppPrinter.Stringify(right);
            }
        
            if (left is double lPlus && right is double rPlus)
            {
                return lPlus + rPlus;
            }

            throw new Exception($"[Line {binary.Operator.Line}] Runtime Error: Operands for '+' must be two numbers or at least one string.");
        }

        if (left is not double l || right is not double r)
        {
            throw new Exception($"[Line {binary.Operator.Line}] Runtime Error: Operands must be numbers.");
        }
    
        return binary.Operator.Type switch
        {
            TokenType.Minus => l - r,
            TokenType.Star => l * r,
            TokenType.Greater => l > r,
            TokenType.GreaterEqual => l >= r,
            TokenType.Less => l < r,
            TokenType.LessEqual => l <= r,
            TokenType.EqualEqual => object.Equals(left, right),
            TokenType.BangEqual => !object.Equals(left, right),
            TokenType.Slash => r == 0 
                ? throw new Exception($"[Line {binary.Operator.Line}] Runtime Error: Division by zero.") 
                : l / r,
            TokenType.Modulo => r == 0 
                ? throw new Exception($"[Line {binary.Operator.Line}] Runtime Error: Division by zero.") 
                : l % r,
            _ => throw new Exception($"[Line {binary.Operator.Line}] Runtime Error: Unknown operator '{binary.Operator.Lexeme}'.")
        };
    }
}

public static class LppPrinter
{
    public static string Stringify(object? obj)
    {
        if (obj == null) return "null";
        if (obj is List<object?> list)
        {
            return "[" + string.Join(", ", list.Select(Stringify)) + "]";
        }
        return obj.ToString() ?? "null";
    }
}

public class BuiltinPrint : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count == 0)
        {
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine(string.Join(" ", arguments.Select(LppPrinter.Stringify)));
        }
        return null;
    }
}

public class BuiltinClear : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        Console.Clear();
        return null;
    }
}

public class BuiltinSleep : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double sleepTime)
        {
            if (sleepTime < 0)
            {
                throw new Exception("Runtime Error: sleep() duration cannot be negative.");
            }
            Thread.Sleep((int)(sleepTime * 1000));
        }
        else
        {
            throw new Exception("Runtime Error: sleep() requires a number argument.");
        }
        return null;
    }
}

public class BuiltinRandom : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count == 1 && arguments[0] is double max)
        {
            return (double)Random.Shared.Next(1, (int)max + 1);
        }
        
        if (arguments.Count == 2 && arguments[0] is double min && arguments[1] is double max2)
        {
            return (double)Random.Shared.Next((int)min, (int)max2 + 1);
        }

        throw new Exception("Runtime Error: random() requires 1 or 2 number arguments.");
    }
}

public class BuiltinInput : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count > 0 && arguments[0] != null)
        {
            Console.Write(arguments[0]);
        }
        string? line = Console.ReadLine();

        if (double.TryParse(line, System.Globalization.CultureInfo.InvariantCulture, out double num))
        {
            return num;
        }

        return line ?? "";
    }
}

public class BuiltinLen : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list) return (double)list.Count;
        if (arguments[0] is string str) return (double)str.Length;
        throw new Exception("Runtime Error: len() expects an array or string.");
    }
}

public class BuiltinPush : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list)
        {
            list.Add(arguments[1]);
            return (double)list.Count;
        }
        throw new Exception("Runtime Error: push() expects an array as first argument.");
    }
}
