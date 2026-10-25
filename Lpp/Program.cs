using Lpp;

Directory.SetCurrentDirectory(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));

string[] cmdArgs = System.Environment.GetCommandLineArgs();
string? targetFile = null;

// Check if a file was provided via command-line arguments
for (int i = 1; i < cmdArgs.Length; i++)
{
    if (cmdArgs[i].EndsWith(".lpp") && File.Exists(cmdArgs[i]))
    {
        targetFile = cmdArgs[i];
        break;
    }
}

targetFile ??= File.Exists("source.lpp") ? "source.lpp" : null;

var interpreter = new Interpreter();

if (targetFile != null)
{
    string code = File.ReadAllText(targetFile);
    RunCode(code, interpreter);
}
else
{
    RunRepl(interpreter);
}

static void RunCode(string code, Interpreter interpreter)
{
    try
    {
        var lexer = new Lexer(code);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var statements = parser.Parse();
        interpreter.Interpret(statements);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Lpp Error] {ex.Message}");
        Console.ResetColor();
    }
}

static void RunRepl(Interpreter interpreter)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("=================================================");
    Console.WriteLine("        LPP INTERACTIVE SHELL (REPL 1.0)         ");
    Console.WriteLine("    Type 'exit()' or press Ctrl+C to quit        ");
    Console.WriteLine("=================================================");
    Console.ResetColor();

    while (true)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(">>> ");
        Console.ResetColor();

        string? line = Console.ReadLine();
        if (line == null || line.Trim() == "exit()")
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(line)) continue;

        // Automatically append ';' to execute single-line expressions as statements
        if (!line.TrimEnd().EndsWith(";") && !line.TrimEnd().EndsWith("}"))
        {
            line += ";";
        }

        RunCode(line, interpreter);
    }
}
