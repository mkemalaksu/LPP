using System.Diagnostics;
using System.Text.Json;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;

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

public class BreakException : Exception { }

public class ContinueException : Exception { }

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

    public LppFunction Bind(LppInstance instance)
    {
        var env = new Environment(closure);
        env.Define("this", instance);
        return new LppFunction(declaration, env);
    }
}

public class LppClass : ICallable
{
    public string Name { get; }
    public Dictionary<string, LppFunction> Methods { get; }

    public LppClass(string name, Dictionary<string, LppFunction> methods)
    {
        Name = name;
        Methods = methods;
    }

    public int Arity => Methods.TryGetValue("init", out var init) ? init.Arity : 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        var instance = new LppInstance(this);
        if (Methods.TryGetValue("init", out var init))
        {
            init.Bind(instance).Call(interpreter, arguments);
        }
        return instance;
    }

    public override string ToString() => $"<class {Name}>";
}

public class LppInstance
{
    public LppClass Class { get; }
    public Dictionary<string, object?> Fields { get; } = new();

    public LppInstance(LppClass @class)
    {
        Class = @class;
    }

    public object? Get(string name)
    {
        if (Fields.TryGetValue(name, out var val)) return val;
        if (Class.Methods.TryGetValue(name, out var method)) return method.Bind(this);
        throw new Exception($"Runtime Error: Undefined property '{name}' on instance of '{Class.Name}'.");
    }

    public void Set(string name, object? value)
    {
        Fields[name] = value;
    }

    public override string ToString() => $"<instance of {Class.Name}>";
}

public class Interpreter
{
    private Environment environment = new();

    public Interpreter()
    {
        // Basic I/O & System
        environment.Define("print", new BuiltinPrint());
        environment.Define("input", new BuiltinInput());
        environment.Define("clear", new BuiltinClear());
        environment.Define("sleep", new BuiltinSleep());
        environment.Define("time", new BuiltinTime());
        environment.Define("now", new BuiltinNow());
        environment.Define("timestamp", new BuiltinTimestamp());
        environment.Define("color", new BuiltinColor());
        environment.Define("reset_color", new BuiltinResetColor());
        environment.Define("beep", new BuiltinBeep());
        environment.Define("exec", new BuiltinExec());
        environment.Define("args", new BuiltinArgs());
        environment.Define("env", new BuiltinEnv());
        environment.Define("exit", new BuiltinExit());
        environment.Define("assert", new BuiltinAssert());
        environment.Define("spawn", new BuiltinSpawn());

        // File System
        environment.Define("read_file", new BuiltinReadFile());
        environment.Define("write_to_file", new BuiltinWriteToFile());
        environment.Define("file_exists", new BuiltinFileExists());
        environment.Define("list_dir", new BuiltinListDir());
        environment.Define("delete_file", new BuiltinDeleteFile());
        environment.Define("create_dir", new BuiltinCreateDir());
        environment.Define("include", new BuiltinInclude());

        // Network & HTTP
        environment.Define("http_get", new BuiltinHttpGet());
        environment.Define("http_post", new BuiltinHttpPost());
        environment.Define("http_request", new BuiltinHttpRequest());

        // JSON & Dictionaries
        environment.Define("json_parse", new BuiltinJsonParse());
        environment.Define("json_stringify", new BuiltinJsonStringify());
        environment.Define("keys", new BuiltinKeys());
        environment.Define("values", new BuiltinValues());
        environment.Define("has_key", new BuiltinHasKey());
        environment.Define("delete", new BuiltinDelete());

        // Array & Functional
        environment.Define("len", new BuiltinLen());
        environment.Define("push", new BuiltinPush());
        environment.Define("pop", new BuiltinPop());
        environment.Define("sort", new BuiltinSort());
        environment.Define("reverse", new BuiltinReverse());
        environment.Define("slice", new BuiltinSlice());
        environment.Define("contains", new BuiltinContains());
        environment.Define("index_of", new BuiltinIndexOf());
        environment.Define("map", new BuiltinMap());
        environment.Define("filter", new BuiltinFilter());
        environment.Define("reduce", new BuiltinReduce());
        environment.Define("unique", new BuiltinUnique());
        environment.Define("intersect", new BuiltinIntersect());
        environment.Define("range", new BuiltinRange());
        environment.Define("random_choice", new BuiltinRandomChoice());
        environment.Define("shuffle", new BuiltinShuffle());

        // Math
        environment.Define("random", new BuiltinRandom());
        environment.Define("sqrt", new BuiltinSqrt());
        environment.Define("floor", new BuiltinFloor());
        environment.Define("ceil", new BuiltinCeil());
        environment.Define("abs", new BuiltinAbs());
        environment.Define("pow", new BuiltinPow());
        environment.Define("min", new BuiltinMin());
        environment.Define("max", new BuiltinMax());
        environment.Define("chr", new BuiltinChr());
        environment.Define("ord", new BuiltinOrd());
        environment.Define("PI", Math.PI);
        environment.Define("E", Math.E);

        // String & Regex
        environment.Define("split", new BuiltinSplit());
        environment.Define("join", new BuiltinJoin());
        environment.Define("trim", new BuiltinTrim());
        environment.Define("to_upper", new BuiltinToUpper());
        environment.Define("to_lower", new BuiltinToLower());
        environment.Define("replace", new BuiltinReplace());
        environment.Define("starts_with", new BuiltinStartsWith());
        environment.Define("ends_with", new BuiltinEndsWith());
        environment.Define("regex_match", new BuiltinRegexMatch());
        environment.Define("regex_replace", new BuiltinRegexReplace());

        // Type Conversion
        environment.Define("type", new BuiltinType());
        environment.Define("to_number", new BuiltinToNumber());
        environment.Define("to_string", new BuiltinToString());

        // Crypto & Hash
        environment.Define("md5", new BuiltinMd5());
        environment.Define("sha256", new BuiltinSha256());
        environment.Define("base64_encode", new BuiltinBase64Encode());
        environment.Define("base64_decode", new BuiltinBase64Decode());

        // Clipboard & Live Key & Notification
        environment.Define("key", new BuiltinKey());
        environment.Define("key_available", new BuiltinKeyAvailable());
        environment.Define("clipboard_set", new BuiltinClipboardSet());
        environment.Define("clipboard_get", new BuiltinClipboardGet());
        environment.Define("notify", new BuiltinNotify());
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
            case ClassStmt classStmt:
                var methods = new Dictionary<string, LppFunction>();
                foreach (var method in classStmt.Methods)
                {
                    methods[method.Name] = new LppFunction(method, environment);
                }
                var lppClass = new LppClass(classStmt.Name, methods);
                environment.Define(classStmt.Name, lppClass);
                break;

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

            case BreakStmt:
                throw new BreakException();

            case ContinueStmt:
                throw new ContinueException();

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
                    try
                    {
                        Execute(whileStmt.Body);
                    }
                    catch (ContinueException)
                    {
                        continue;
                    }
                    catch (BreakException)
                    {
                        break;
                    }
                }
                break;

            case ForStmt forStmt:
                Environment forEnv = new Environment(environment);
                Environment previousEnv = environment;
                try
                {
                    environment = forEnv;
                    if (forStmt.Initializer != null)
                    {
                        Execute(forStmt.Initializer);
                    }

                    while (forStmt.Condition == null || IsTruthy(Evaluate(forStmt.Condition)))
                    {
                        try
                        {
                            Execute(forStmt.Body);
                        }
                        catch (ContinueException)
                        {
                            // On continue, increment still executes
                        }
                        catch (BreakException)
                        {
                            break;
                        }

                        if (forStmt.Increment != null)
                        {
                            Evaluate(forStmt.Increment);
                        }
                    }
                }
                finally
                {
                    environment = previousEnv;
                }
                break;

            case ForInStmt forInStmt:
                object? iterObj = Evaluate(forInStmt.Iterable);
                if (iterObj is List<object?> list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Environment loopEnv = new Environment(environment);
                        if (forInStmt.Var2 != null)
                        {
                            loopEnv.Define(forInStmt.Var1, (double)i);
                            loopEnv.Define(forInStmt.Var2, list[i]);
                        }
                        else
                        {
                            loopEnv.Define(forInStmt.Var1, list[i]);
                        }

                        try
                        {
                            ExecuteBlock(new List<Stmt> { forInStmt.Body }, loopEnv);
                        }
                        catch (ContinueException) { }
                        catch (BreakException) { break; }
                    }
                }
                else if (iterObj is Dictionary<string, object?> dict)
                {
                    foreach (var (k, v) in dict)
                    {
                        Environment loopEnv = new Environment(environment);
                        loopEnv.Define(forInStmt.Var1, k);
                        if (forInStmt.Var2 != null)
                        {
                            loopEnv.Define(forInStmt.Var2, v);
                        }

                        try
                        {
                            ExecuteBlock(new List<Stmt> { forInStmt.Body }, loopEnv);
                        }
                        catch (ContinueException) { }
                        catch (BreakException) { break; }
                    }
                }
                else
                {
                    throw new Exception("Runtime Error: Can only iterate over arrays or dictionaries in for-in.");
                }
                break;

            case TryCatchStmt tryCatch:
                try
                {
                    Execute(tryCatch.TryBlock);
                }
                catch (Exception ex) when (ex is not ReturnException and not BreakException and not ContinueException)
                {
                    Environment catchEnv = new Environment(environment);
                    catchEnv.Define(tryCatch.CatchVar, ex.Message);
                    ExecuteBlock(new List<Stmt> { tryCatch.CatchBlock }, catchEnv);
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
            LogicalExpr logical => EvaluateLogical(logical),
            UnaryExpr unary => EvaluateUnary(unary),
            TernaryExpr ternary => IsTruthy(Evaluate(ternary.Condition)) ? Evaluate(ternary.ThenBranch) : Evaluate(ternary.ElseBranch),
            CallExpr call => EvaluateCall(call),
            AssignExpr assign => EvaluateAssign(assign),
            ArrayExpr arrayExpr => EvaluateArray(arrayExpr),
            DictionaryExpr dictExpr => EvaluateDictionary(dictExpr),
            DotExpr dotExpr => EvaluateDot(dotExpr),
            DotAssignExpr dotAssign => EvaluateDotAssign(dotAssign),
            InterpolatedStringExpr interp => EvaluateInterpolatedString(interp),
            AnonymousFunctionExpr anon => new LppFunction(new FunctionStmt("", anon.Parameters, anon.Body), environment),
            NewExpr newExpr => EvaluateNew(newExpr),
            ThisExpr => environment.Get("this"),
            IndexExpr indexExpr => EvaluateIndex(indexExpr),
            IndexAssignExpr indexAssign => EvaluateIndexAssign(indexAssign),
            _ => throw new Exception($"Unknown expression type {expr.GetType()}")
        };
    }

    private object? EvaluateNew(NewExpr newExpr)
    {
        object? callee = environment.Get(newExpr.ClassName);
        if (callee is not LppClass cls)
        {
            throw new Exception($"Runtime Error: '{newExpr.ClassName}' is not a class.");
        }
        var args = newExpr.Arguments.Select(Evaluate).ToList();
        return cls.Call(this, args);
    }

    private object? EvaluateDictionary(DictionaryExpr dictExpr)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var (kExpr, vExpr) in dictExpr.Pairs)
        {
            object? k = Evaluate(kExpr);
            object? v = Evaluate(vExpr);
            dict[LppPrinter.Stringify(k)] = v;
        }
        return dict;
    }

    private object? EvaluateDot(DotExpr dotExpr)
    {
        object? target = Evaluate(dotExpr.Target);
        if (target is Dictionary<string, object?> dict)
        {
            return dict.TryGetValue(dotExpr.Property, out var val) ? val : null;
        }
        if (target is LppInstance inst)
        {
            return inst.Get(dotExpr.Property);
        }
        throw new Exception($"Runtime Error: Cannot access property '{dotExpr.Property}'.");
    }

    private object? EvaluateDotAssign(DotAssignExpr dotAssign)
    {
        object? target = Evaluate(dotAssign.Target);
        object? val = Evaluate(dotAssign.Value);
        if (target is Dictionary<string, object?> dict)
        {
            dict[dotAssign.Property] = val;
            return val;
        }
        if (target is LppInstance inst)
        {
            inst.Set(dotAssign.Property, val);
            return val;
        }
        throw new Exception($"Runtime Error: Cannot assign property '{dotAssign.Property}'.");
    }

    private object? EvaluateInterpolatedString(InterpolatedStringExpr interp)
    {
        var sb = new StringBuilder();
        foreach (var part in interp.Parts)
        {
            sb.Append(LppPrinter.Stringify(Evaluate(part)));
        }
        return sb.ToString();
    }

    private object? EvaluateLogical(LogicalExpr logical)
    {
        object? left = Evaluate(logical.Left);

        if (logical.Operator.Type == TokenType.Or)
        {
            if (IsTruthy(left)) return left;
        }
        else // TokenType.And
        {
            if (!IsTruthy(left)) return left;
        }

        return Evaluate(logical.Right);
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

        if (target is Dictionary<string, object?> dict)
        {
            string key = LppPrinter.Stringify(indexVal);
            return dict.TryGetValue(key, out var val) ? val : null;
        }

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

        throw new Exception("Runtime Error: Index operator can only be used on arrays, strings, or dictionaries.");
    }

    private object? EvaluateIndexAssign(IndexAssignExpr indexAssign)
    {
        object? target = Evaluate(indexAssign.Target);
        object? indexVal = Evaluate(indexAssign.Index);
        object? val = Evaluate(indexAssign.Value);

        if (target is Dictionary<string, object?> dict)
        {
            string key = LppPrinter.Stringify(indexVal);
            dict[key] = val;
            return val;
        }

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

        throw new Exception("Runtime Error: Can only assign to elements of an array or dictionary.");
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
            throw new Exception("Runtime Error: Can only call functions or classes.");
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
        if (obj is Dictionary<string, object?> dict)
        {
            return "{ " + string.Join(", ", dict.Select(kv => $"\"{kv.Key}\": {Stringify(kv.Value)}")) + " }";
        }
        if (obj is LppInstance inst) return inst.ToString();
        if (obj is LppClass cls) return cls.ToString();
        return obj.ToString() ?? "null";
    }
}

// ================= BUILT-IN FUNCTIONS =================

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
        if (arguments[0] is Dictionary<string, object?> dict) return (double)dict.Count;
        if (arguments[0] is string str) return (double)str.Length;
        throw new Exception("Runtime Error: len() expects an array, dictionary or string.");
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

public class BuiltinPop : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list)
        {
            if (list.Count == 0) throw new Exception("Runtime Error: pop() called on an empty array.");
            object? last = list[^1];
            list.RemoveAt(list.Count - 1);
            return last;
        }
        throw new Exception("Runtime Error: pop() expects an array.");
    }
}

public class BuiltinWriteToFile : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] != null && arguments[1] != null)
        {
            string path = LppPrinter.Stringify(arguments[0]);
            string content = LppPrinter.Stringify(arguments[1]);
            File.WriteAllText(path, content);
            return null;
        }
        throw new Exception("Runtime Error: write_to_file() expects (path, content).");
    }
}

public class BuiltinReadFile : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] != null)
        {
            string path = LppPrinter.Stringify(arguments[0]);
            if (!File.Exists(path)) throw new Exception($"Runtime Error: File not found: '{path}'.");
            return File.ReadAllText(path);
        }
        throw new Exception("Runtime Error: read_file() expects a file path.");
    }
}

public class BuiltinTime : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }
}

public class BuiltinNow : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class BuiltinTimestamp : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}

public class BuiltinHttpGet : ICallable
{
    private static readonly HttpClient client = new();
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string url = LppPrinter.Stringify(arguments[0]);
        return client.GetStringAsync(url).GetAwaiter().GetResult();
    }
}

public class BuiltinHttpPost : ICallable
{
    private static readonly HttpClient client = new();
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string url = LppPrinter.Stringify(arguments[0]);
        string body = LppPrinter.Stringify(arguments[1]);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var res = client.PostAsync(url, content).GetAwaiter().GetResult();
        return res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }
}

public class BuiltinHttpRequest : ICallable
{
    private static readonly HttpClient client = new();
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is not Dictionary<string, object?> opts)
            throw new Exception("Runtime Error: http_request() expects an options dictionary.");

        string url = opts.TryGetValue("url", out var u) ? LppPrinter.Stringify(u) : throw new Exception("http_request() requires 'url'.");
        string method = opts.TryGetValue("method", out var m) ? LppPrinter.Stringify(m).ToUpper() : "GET";

        var req = new HttpRequestMessage(new HttpMethod(method), url);

        if (opts.TryGetValue("headers", out var hObj) && hObj is Dictionary<string, object?> headers)
        {
            foreach (var (k, v) in headers)
            {
                req.Headers.TryAddWithoutValidation(k, LppPrinter.Stringify(v));
            }
        }

        if (opts.TryGetValue("body", out var bObj) && bObj != null)
        {
            string bStr = LppPrinter.Stringify(bObj);
            req.Content = new StringContent(bStr, Encoding.UTF8, "application/json");
        }

        var res = client.SendAsync(req).GetAwaiter().GetResult();
        string body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        var resHeaders = new Dictionary<string, object?>();
        foreach (var header in res.Headers) resHeaders[header.Key] = string.Join(", ", header.Value);
        foreach (var header in res.Content.Headers) resHeaders[header.Key] = string.Join(", ", header.Value);

        return new Dictionary<string, object?>
        {
            ["status"] = (double)(int)res.StatusCode,
            ["body"] = body,
            ["headers"] = resHeaders
        };
    }
}

public static class LppJson
{
    public static object? ParseElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ParseElement).ToList<object?>(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ParseElement(p.Value)),
            _ => null
        };
    }
}

public class BuiltinJsonParse : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string json = LppPrinter.Stringify(arguments[0]);
        using var doc = JsonDocument.Parse(json);
        return LppJson.ParseElement(doc.RootElement);
    }
}

public class BuiltinJsonStringify : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count == 0) return "null";
        bool pretty = arguments.Count > 1 && arguments[1] is true;
        return JsonSerializer.Serialize(arguments[0], new JsonSerializerOptions { WriteIndented = pretty });
    }
}

public class BuiltinKeys : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is Dictionary<string, object?> dict)
        {
            return dict.Keys.Cast<object?>().ToList();
        }
        throw new Exception("Runtime Error: keys() expects a dictionary.");
    }
}

public class BuiltinValues : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is Dictionary<string, object?> dict)
        {
            return dict.Values.ToList();
        }
        throw new Exception("Runtime Error: values() expects a dictionary.");
    }
}

public class BuiltinHasKey : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is Dictionary<string, object?> dict)
        {
            string key = LppPrinter.Stringify(arguments[1]);
            return dict.ContainsKey(key);
        }
        throw new Exception("Runtime Error: has_key() expects (dictionary, key).");
    }
}

public class BuiltinDelete : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is Dictionary<string, object?> dict)
        {
            string key = LppPrinter.Stringify(arguments[1]);
            return dict.Remove(key);
        }
        throw new Exception("Runtime Error: delete() expects (dictionary, key).");
    }
}

public class BuiltinSort : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list)
        {
            var sorted = list.OrderBy(x => x is double d ? d : double.MaxValue).ToList();
            list.Clear();
            list.AddRange(sorted);
            return list;
        }
        throw new Exception("Runtime Error: sort() expects an array.");
    }
}

public class BuiltinReverse : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list)
        {
            list.Reverse();
            return list;
        }
        throw new Exception("Runtime Error: reverse() expects an array.");
    }
}

public class BuiltinSlice : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count >= 2 && arguments[0] is List<object?> list && arguments[1] is double start)
        {
            int s = (int)start;
            int count = arguments.Count >= 3 && arguments[2] is double c ? (int)c : list.Count - s;
            if (s < 0) s = 0;
            if (s + count > list.Count) count = list.Count - s;
            return list.GetRange(s, Math.Max(0, count));
        }
        throw new Exception("Runtime Error: slice() expects (array, start, [count]).");
    }
}

public class BuiltinContains : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list) return list.Contains(arguments[1]);
        if (arguments[0] is string str) return str.Contains(LppPrinter.Stringify(arguments[1]));
        throw new Exception("Runtime Error: contains() expects an array or string.");
    }
}

public class BuiltinIndexOf : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list) return (double)list.IndexOf(arguments[1]);
        if (arguments[0] is string str) return (double)str.IndexOf(LppPrinter.Stringify(arguments[1]));
        throw new Exception("Runtime Error: index_of() expects an array or string.");
    }
}

public class BuiltinMap : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list && arguments[1] is ICallable fn)
        {
            return list.Select(x => fn.Call(interpreter, new List<object?> { x })).ToList();
        }
        throw new Exception("Runtime Error: map() expects (array, function).");
    }
}

public class BuiltinFilter : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list && arguments[1] is ICallable fn)
        {
            var result = new List<object?>();
            foreach (var item in list)
            {
                var ok = fn.Call(interpreter, new List<object?> { item });
                if (ok != null && ok is not false) result.Add(item);
            }
            return result;
        }
        throw new Exception("Runtime Error: filter() expects (array, function).");
    }
}

public class BuiltinReduce : ICallable
{
    public int Arity => 3;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list && arguments[1] is ICallable fn)
        {
            object? accum = arguments[2];
            foreach (var item in list)
            {
                accum = fn.Call(interpreter, new List<object?> { accum, item });
            }
            return accum;
        }
        throw new Exception("Runtime Error: reduce() expects (array, function, initialValue).");
    }
}

public class BuiltinUnique : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list) return list.Distinct().ToList();
        throw new Exception("Runtime Error: unique() expects an array.");
    }
}

public class BuiltinIntersect : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> l1 && arguments[1] is List<object?> l2)
        {
            return l1.Intersect(l2).ToList();
        }
        throw new Exception("Runtime Error: intersect() expects two arrays.");
    }
}

public class BuiltinRange : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count == 1 && arguments[0] is double end)
        {
            var list = new List<object?>();
            for (double i = 0; i < end; i++) list.Add(i);
            return list;
        }
        if (arguments.Count >= 2 && arguments[0] is double start && arguments[1] is double end2)
        {
            double step = arguments.Count >= 3 && arguments[2] is double s ? s : 1.0;
            var list = new List<object?>();
            for (double i = start; i < end2; i += step) list.Add(i);
            return list;
        }
        throw new Exception("Runtime Error: range() expects 1 to 3 numbers.");
    }
}

public class BuiltinRandomChoice : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list && list.Count > 0)
        {
            return list[Random.Shared.Next(list.Count)];
        }
        throw new Exception("Runtime Error: random_choice() expects a non-empty array.");
    }
}

public class BuiltinShuffle : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Shared.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
            return list;
        }
        throw new Exception("Runtime Error: shuffle() expects an array.");
    }
}

public class BuiltinSplit : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is string text && arguments[1] is string separator)
        {
            return text.Split(separator).Cast<object?>().ToList();
        }
        throw new Exception("Runtime Error: split() expects (text: string, separator: string).");
    }
}

public class BuiltinJoin : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is List<object?> list && arguments[1] is string separator)
        {
            return string.Join(separator, list.Select(LppPrinter.Stringify));
        }
        throw new Exception("Runtime Error: join() expects (array, separator: string).");
    }
}

public class BuiltinTrim : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return LppPrinter.Stringify(arguments[0]).Trim();
    }
}

public class BuiltinToUpper : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return LppPrinter.Stringify(arguments[0]).ToUpper();
    }
}

public class BuiltinToLower : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return LppPrinter.Stringify(arguments[0]).ToLower();
    }
}

public class BuiltinReplace : ICallable
{
    public int Arity => 3;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string text = LppPrinter.Stringify(arguments[0]);
        string oldVal = LppPrinter.Stringify(arguments[1]);
        string newVal = LppPrinter.Stringify(arguments[2]);
        return text.Replace(oldVal, newVal);
    }
}

public class BuiltinStartsWith : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return LppPrinter.Stringify(arguments[0]).StartsWith(LppPrinter.Stringify(arguments[1]));
    }
}

public class BuiltinEndsWith : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return LppPrinter.Stringify(arguments[0]).EndsWith(LppPrinter.Stringify(arguments[1]));
    }
}

public class BuiltinRegexMatch : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string pattern = LppPrinter.Stringify(arguments[0]);
        string input = LppPrinter.Stringify(arguments[1]);
        return Regex.IsMatch(input, pattern);
    }
}

public class BuiltinRegexReplace : ICallable
{
    public int Arity => 3;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string pattern = LppPrinter.Stringify(arguments[0]);
        string input = LppPrinter.Stringify(arguments[1]);
        string replacement = LppPrinter.Stringify(arguments[2]);
        return Regex.Replace(input, pattern, replacement);
    }
}

public class BuiltinSqrt : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double num)
        {
            if (num < 0) throw new Exception("Runtime Error: sqrt() cannot take a negative number.");
            return Math.Sqrt(num);
        }
        throw new Exception("Runtime Error: sqrt() expects a number.");
    }
}

public class BuiltinFloor : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double num) return Math.Floor(num);
        throw new Exception("Runtime Error: floor() expects a number.");
    }
}

public class BuiltinCeil : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double num) return Math.Ceiling(num);
        throw new Exception("Runtime Error: ceil() expects a number.");
    }
}

public class BuiltinAbs : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double num) return Math.Abs(num);
        throw new Exception("Runtime Error: abs() expects a number.");
    }
}

public class BuiltinPow : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double b && arguments[1] is double exp) return Math.Pow(b, exp);
        throw new Exception("Runtime Error: pow() expects (base: number, exponent: number).");
    }
}

public class BuiltinMin : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double a && arguments[1] is double b) return Math.Min(a, b);
        throw new Exception("Runtime Error: min() expects two numbers.");
    }
}

public class BuiltinMax : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double a && arguments[1] is double b) return Math.Max(a, b);
        throw new Exception("Runtime Error: max() expects two numbers.");
    }
}

public class BuiltinChr : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double n) return ((char)(int)n).ToString();
        throw new Exception("Runtime Error: chr() expects a number.");
    }
}

public class BuiltinOrd : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string s = LppPrinter.Stringify(arguments[0]);
        if (s.Length > 0) return (double)s[0];
        throw new Exception("Runtime Error: ord() expects a non-empty string.");
    }
}

public class BuiltinMd5 : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        byte[] input = Encoding.UTF8.GetBytes(LppPrinter.Stringify(arguments[0]));
        byte[] hash = MD5.HashData(input);
        return Convert.ToHexString(hash).ToLower();
    }
}

public class BuiltinSha256 : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        byte[] input = Encoding.UTF8.GetBytes(LppPrinter.Stringify(arguments[0]));
        byte[] hash = SHA256.HashData(input);
        return Convert.ToHexString(hash).ToLower();
    }
}

public class BuiltinBase64Encode : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        byte[] input = Encoding.UTF8.GetBytes(LppPrinter.Stringify(arguments[0]));
        return Convert.ToBase64String(input);
    }
}

public class BuiltinBase64Decode : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        byte[] bytes = Convert.FromBase64String(LppPrinter.Stringify(arguments[0]));
        return Encoding.UTF8.GetString(bytes);
    }
}

public class BuiltinType : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        var val = arguments[0];
        if (val == null) return "null";
        if (val is double) return "number";
        if (val is string) return "string";
        if (val is bool) return "boolean";
        if (val is List<object?>) return "array";
        if (val is Dictionary<string, object?>) return "dictionary";
        if (val is LppInstance) return "instance";
        if (val is LppClass) return "class";
        if (val is ICallable) return "function";
        return val.GetType().Name;
    }
}

public class BuiltinToNumber : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is double d) return d;
        if (double.TryParse(LppPrinter.Stringify(arguments[0]), System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            return num;
        }
        return null;
    }
}

public class BuiltinToString : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return LppPrinter.Stringify(arguments[0]);
    }
}

public class BuiltinColor : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string name = LppPrinter.Stringify(arguments[0]).ToLower();
        Console.ForegroundColor = name switch
        {
            "red" => ConsoleColor.Red,
            "green" => ConsoleColor.Green,
            "blue" => ConsoleColor.Blue,
            "yellow" => ConsoleColor.Yellow,
            "cyan" => ConsoleColor.Cyan,
            "magenta" => ConsoleColor.Magenta,
            "white" => ConsoleColor.White,
            "black" => ConsoleColor.Black,
            "gray" => ConsoleColor.Gray,
            _ => ConsoleColor.White
        };
        return null;
    }
}

public class BuiltinResetColor : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        Console.ResetColor();
        return null;
    }
}

public class BuiltinBeep : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        int freq = arguments.Count > 0 && arguments[0] is double f ? (int)f : 800;
        int dur = arguments.Count > 1 && arguments[1] is double d ? (int)d : 200;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Console.Beep(freq, dur);
            }
            else
            {
                Console.Write("\a");
            }
        }
        catch
        {
            Console.Write("\a");
        }
        return null;
    }
}

public class BuiltinExec : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string cmd = LppPrinter.Stringify(arguments[0]);
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/zsh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {cmd}" : $"-c \"{cmd.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        string output = p?.StandardOutput.ReadToEnd() ?? "";
        p?.WaitForExit();
        return output;
    }
}

public class BuiltinArgs : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        var cmdArgs = System.Environment.GetCommandLineArgs();
        var list = new List<object?>();
        for (int i = 1; i < cmdArgs.Length; i++) list.Add(cmdArgs[i]);
        return list;
    }
}

public class BuiltinEnv : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string key = LppPrinter.Stringify(arguments[0]);
        return System.Environment.GetEnvironmentVariable(key);
    }
}

public class BuiltinFileExists : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string path = LppPrinter.Stringify(arguments[0]);
        return File.Exists(path);
    }
}

public class BuiltinListDir : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string path = arguments.Count > 0 ? LppPrinter.Stringify(arguments[0]) : ".";
        return Directory.GetFileSystemEntries(path).Select(Path.GetFileName).Cast<object?>().ToList();
    }
}

public class BuiltinDeleteFile : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string path = LppPrinter.Stringify(arguments[0]);
        if (File.Exists(path)) File.Delete(path);
        return null;
    }
}

public class BuiltinCreateDir : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string path = LppPrinter.Stringify(arguments[0]);
        Directory.CreateDirectory(path);
        return null;
    }
}

public class BuiltinSpawn : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] is ICallable fn)
        {
            Task.Run(() =>
            {
                try
                {
                    fn.Call(interpreter, new List<object?>());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Spawn Error] {ex.Message}");
                }
            });
            return null;
        }
        throw new Exception("Runtime Error: spawn() expects a function.");
    }
}

public class BuiltinKey : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (Console.KeyAvailable)
        {
            return Console.ReadKey(true).Key.ToString();
        }
        return null;
    }
}

public class BuiltinKeyAvailable : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        return Console.KeyAvailable;
    }
}

public class BuiltinClipboardSet : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string text = LppPrinter.Stringify(arguments[0]);
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo("pbcopy") { RedirectStandardInput = true, UseShellExecute = false };
                using var p = Process.Start(psi);
                p?.StandardInput.Write(text);
                p?.StandardInput.Close();
                p?.WaitForExit();
            }
        }
        catch { }
        return null;
    }
}

public class BuiltinClipboardGet : ICallable
{
    public int Arity => 0;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo("pbpaste") { RedirectStandardOutput = true, UseShellExecute = false };
                using var p = Process.Start(psi);
                string outStr = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                return outStr;
            }
        }
        catch { }
        return "";
    }
}

public class BuiltinNotify : ICallable
{
    public int Arity => 2;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        string title = LppPrinter.Stringify(arguments[0]);
        string msg = LppPrinter.Stringify(arguments[1]);
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo("osascript", $"-e 'display notification \"{msg.Replace("\"", "\\\"")}\" with title \"{title.Replace("\"", "\\\"")}\"'")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
            }
        }
        catch { }
        return null;
    }
}

public class BuiltinExit : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        int code = arguments.Count > 0 && arguments[0] is double d ? (int)d : 0;
        System.Environment.Exit(code);
        return null;
    }
}

public class BuiltinAssert : ICallable
{
    public int Arity => -1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments.Count == 0 || arguments[0] == null || arguments[0] is false)
        {
            string msg = arguments.Count > 1 ? LppPrinter.Stringify(arguments[1]) : "Assertion failed.";
            throw new Exception($"Assertion Error: {msg}");
        }
        return null;
    }
}

public class BuiltinInclude : ICallable
{
    public int Arity => 1;

    public object? Call(Interpreter interpreter, List<object?> arguments)
    {
        if (arguments[0] != null)
        {
            string path = LppPrinter.Stringify(arguments[0]);
            if (!File.Exists(path))
            {
                throw new Exception($"Runtime Error: Included file not found: '{path}'.");
            }

            string source = File.ReadAllText(path);
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var statements = parser.Parse();
            interpreter.Interpret(statements);
            return null;
        }

        throw new Exception("Runtime Error: include() expects a file path.");
    }
}
