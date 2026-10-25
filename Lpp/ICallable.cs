namespace Lpp;

public interface ICallable
{
    int Arity { get; }
    
    object? Call(Interpreter interpreter, List<object?> arguments);
}