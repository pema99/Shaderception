
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class Parser : UdonSharpBehaviour
{
    // TODO:
    // - Error handling
    // - Variables
    // - Function calls

    public InputField input;
    public Text output;

    public Material heightMat;

    public override void Interact()
    {
        // Lex
        currentLexed = 0;
        lexed = new object[1000];
        Debug.Log(input.text);
        Lex(input.text);

        // Parse
        currentLexed = 0;
        currentParsed = 0;
        parsed = new object[1000];
        Program();
        output.text = "";
        for (int i = 0; i < parsed.Length; i += 2)
        {
            if (parsed[i] == null) break;
            output.text += (parsed[i] + " " + parsed[i + 1]) + "\n";
        }

        // Write to material
        var mat = GetComponent<MeshRenderer>().sharedMaterial;
        WriteProgramToMaterial(mat);
        WriteProgramToMaterial(heightMat);
        float[] bin = mat.GetFloatArray("_Program");
        string res = "";
        for (int i = 0; i < bin.Length; i++)
        {
            if (i % 2 == 0 && bin[i] == 0) break;
            res += bin[i] + ", ";
        }
        Debug.Log(res);
    }

    // Write to GPU data
    float FuncIdentToIndex(string ident)
    {
        switch (ident.ToLower())
        {
            case "log":        return 1;
            case "log2":       return 2;
            case "sin":        return 3;     
            case "cos":        return 4;  
            case "tan":        return 5;     
            case "asin":       return 6;     
            case "acos":       return 7;     
            case "atan":       return 8;     
            case "pow":        return 9;            
            case "exp":        return 10;  
            case "exp2":       return 11;       
            case "sqrt":       return 12;       
            case "rsqrt":      return 13;       
            case "abs":        return 14;   
            case "sign":       return 15;        
            case "floor":      return 16;        
            case "ceil":       return 17;        
            case "frac":       return 18;        
            case "mod":        return 19;                
            case "min":        return 20;            
            case "max":        return 21;            
            case "clamp":      return 22;                    
            case "lerp":       return 23;                    
            case "step":       return 24;            
            case "smoothstep": return 25;                            
            default:      return 0;
        }
    }

    void WriteProgramToMaterial(Material mat)
    {
        float[] program = new float[1000];

        for (int i = 0; i < parsed.Length; i += 2)
        {
            if (parsed[i] == null) program[i] = 0;

            switch (parsed[i])
            {
                case "PUSHCONST":
                    program[i] = 1;
                    program[i+1] = (float)parsed[i+1];
                    break;
                case "PUSHVAR":
                    program[i] = 2;
                    program[i+1] = (float)((int)((string)parsed[i+1])[0]);
                    break;
                case "BINOP":
                    program[i] = 3;
                    program[i+1] = (float)((int)parsed[i+1]);
                    break;
                case "UNOP":
                    program[i] = 4;
                    program[i+1] = (float)((int)parsed[i+1]);
                    break;
                case "CALL":
                    program[i] = 5;
                    program[i+1] = FuncIdentToIndex((string)parsed[i+1]);
                    break;
                case "SETVAR":
                    program[i] = 6;
                    program[i+1] = (float)((int)((string)parsed[i+1])[0]);
                    break;
            }
        }

        mat.SetFloatArray("_Program", program);
    }

    // Parsing
    int currentParsed = 0;
    object[] parsed;

    void Log(string instr, object op)
    {
        parsed[currentParsed++] = instr;
        parsed[currentParsed++] = op;
    }

    object Peek()
    {
        return lexed[currentLexed];
    }

    object PeekNext()
    {
        return lexed[currentLexed+1];
    }

    object Eat()
    {
        return lexed[currentLexed++];
    }

    bool Match(object tok)
    {
        return tok.Equals(Peek());
    }

    bool IsAtEnd()
    {
        return (currentLexed >= lexed.Length - 1) || (Peek() == null);
    }

    void Program()
    {
        while (!IsAtEnd() && !Statement())
        {
            Eat(); // ;
        }
    }

    // Returns: Is final statement?
    bool Statement()
    {
        if (Peek().GetType() == typeof(string)) // potential keywords
        {
            string ident = (string)Peek();
            switch (ident) // keywords
            {
                case "let": Assignment(); return false;
                case "fun": FuncDef();    return false;
                default:    Expression(); return true;
            }
        }
        else
        {
            Expression();
            return true;
        }
    }

    void Assignment()
    {
        Eat(); // let
        object ident = Eat();
        Eat(); // =
        Expression();
        Log("SETVAR", ident);
    }

    // TODO: Ftab
    void FuncDef()
    {
        Eat(); // fun
        object ident = Eat();
        Eat(); // (
        // ...
        Eat(); // )
        Eat(); // =
        Expression();
    }

    [RecursiveMethod]
    void FuncCall()
    {
        object ident = Eat(); // ident
        Eat(); // lparen
        Expression();
        while (Match(','))
        {
            Eat();
            Expression();
        }
        Eat(); // rparen
        Log("CALL", ident);
    }

    [RecursiveMethod]
    void Expression()
    {
        Term();

        while (Match('+') || Match('-'))
        {
            object tok = Eat();
            Term();
            Log("BINOP", tok);
            if (IsAtEnd()) return;
        }
    }

    [RecursiveMethod]
    void Term()
    {
        Factor();

        while (Match('*') || Match('/'))
        {
            object tok = Eat();
            Factor();
            Log("BINOP", tok);
            if (IsAtEnd()) return;
        }
    }

    [RecursiveMethod]
    void Factor()
    {
        if (IsAtEnd()) return;

        var type = Peek().GetType();
        if (type == typeof(string)) // identifier
        {
            if ('('.Equals(PeekNext())) // function call
            {
                FuncCall();
            }
            else // var access
            {
                Log("PUSHVAR", Eat());
            }
        }
        else if (type == typeof(float)) // literal
        {
            Log("PUSHCONST", Eat());
        }
        else if (type == typeof(char))
        {
            if (Peek().Equals('(')) // grouped
            {
                Eat();
                Expression();
                Eat();
            }
            else if (Peek().Equals('+') || Peek().Equals('-')) // unary op
            {
                var op = Eat();
                Expression();
                Log("UNOP", op);
            }
        }
    }

    // Lexing
    int currentLexed = 0;
    object[] lexed;

    bool IsAlpha(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    bool IsNumeric(char c)
    {
        return (c >= '0' && c <= '9') || (c == '.');
    }

    bool IsAlphaNumeric(char c)
    {
        return IsAlpha(c) || IsNumeric(c);
    }

    void Lex(string input)
    {
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];

            // identifier
            if (IsAlpha(c))
            {
                string ident = "";
                while (i < input.Length && IsAlphaNumeric(input[i]))
                {
                    ident += input[i++];
                }
                lexed[currentLexed++] = ident;
            }

            // numeric
            else if (IsNumeric(c))
            {
                string ident = "";
                while (i < input.Length && IsNumeric(input[i]))
                {
                    ident += input[i++];
                }
                float res;
                if (float.TryParse(ident, out res))
                    lexed[currentLexed++] = res;
            }

            // whitespace
            else if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                i++;
            }

            // single token, skip whitespace
            else
            {
                lexed[currentLexed++] = c;
                i++;
            }
        }
    }
}
