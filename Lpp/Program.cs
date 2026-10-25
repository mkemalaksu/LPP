using Lpp;

string code = """
              local arr = [123,1,76];
              print(arr[0]);
                    local eksi = -5;
                    function topla(a,b){
                        return a + b;
                    }
                    print(topla(eksi, 50));
              
              """;

// 1. Lexer
    ;var lexer = new Lexer(code);
var tokens = lexer.Tokenize();

// 2. Parser
var parser = new Parser(tokens);
var statements = parser.Parse();

var interpreter =  new Interpreter();
interpreter.Interpret(statements); 
