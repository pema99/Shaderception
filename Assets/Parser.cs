
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class Parser : UdonSharpBehaviour
{
    // TODO:
    // - Error handling
    // - Get get pixel from last frames data
    // - Get audiolink value
    // - Swizzle assignments
    // - Function calls (maybe)

    public InputField input;
    public Text output;

    public Material screenMat;
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
        labelCount = 0;
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
        WriteProgramToMaterial(screenMat);
        WriteProgramToMaterial(heightMat);
        Vector4[] bin = screenMat.GetVectorArray("_Program");
        string res = "";
        for (int i = 0; i < bin.Length; i++)
        {
            if (i % 2 == 0 && bin[i] == Vector4.zero) break;
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
            case "float2":     return 26;
            case "float3":     return 27;
            case "float4":     return 28;
            case "swizzle":    return 29;                            
            default:           return 0;
        }
    }

    float BinOpToIndex(string op)
    {
        switch (op)
        {
            case "+":  return 1;
            case "-":  return 2;
            case "*":  return 3;
            case "/":  return 4;
            case "<":  return 5;
            case ">":  return 6;
            case "=":  return 7;
            case "<=": return 8;
            case ">=": return 9;
            case "!=": return 10;
            case "&&": return 11;
            case "||": return 12;
            default:   return 0;
        }
    }

    void WriteProgramToMaterial(Material mat)
    {
        Vector4 one = new Vector4(1, 0, 0, 0);

        Vector4[] program = new Vector4[1000];

        for (int i = 0; i < parsed.Length; i += 2)
        {
            if (parsed[i] == null) program[i] = Vector4.zero;

            switch (parsed[i])
            {
                case "PUSHCONST":
                    program[i] = one * 1;
                    program[i+1] = (Vector4)parsed[i+1];
                    break;
                case "PUSHVAR":
                    program[i] = one * 2;
                    program[i+1] = one * (float)((int)((string)parsed[i+1])[0]); // TODO 4 char names
                    break;
                case "BINOP":
                    program[i] = one * 3;
                    program[i+1] = one * BinOpToIndex((string)parsed[i+1]);
                    break;
                case "UNOP":
                    program[i] = one * 4;
                    program[i+1] = one * (float)((int)((string)parsed[i+1])[0]);
                    break;
                case "CALL":
                    program[i] = one * 5;
                    program[i+1] = one * FuncIdentToIndex((string)parsed[i+1]);
                    break;
                case "SETVAR":
                    program[i] = one * 6;
                    program[i+1] = one * (float)((int)((string)parsed[i+1])[0]);
                    if (((string)parsed[i+1]).Contains(".")) // swizzle assignments
                    {
                        string swizzle = ((string)parsed[i+1]).Split('.')[1];
                        string mask = "";
                        for (int j = 0; j < 4; j++)
                        {
                            if (j >= swizzle.Length)
                                break;

                            switch (swizzle[j])
                            {
                                case 'x': case 'r': mask += '1'; break;
                                case 'y': case 'g': mask += '2'; break;
                                case 'z': case 'b': mask += '3'; break;
                                case 'w': case 'a': mask += '4'; break;
                            }
                        }
                        program[i+1].y = float.Parse(mask);
                    }
                    break;
                case "JUMP":
                    program[i] = one * 7;
                    program[i+1] = one * (float)parsed[i+1];
                    break;
                case "CONDJUMP":
                    program[i] = one * 8;
                    program[i+1] = one * (float)parsed[i+1];
                    break;
                case "LABEL":
                    program[i] = one * 9;
                    program[i+1] = Vector4.zero;
                    break;
            }
        }

        mat.SetVectorArray("_Program", program);
    }

    // Linking
    void Link()
    {
        for (int i = 0; i < parsed.Length; i += 2)
        {
            if (parsed[i] == null) break;

            if (parsed[i] == "JUMP" || parsed[i] == "CONDJUMP")
            {
                string label = (string)parsed[i+1];
                for (int j = 0; j < parsed.Length; j++)
                {
                    if (parsed[j] == null) break;

                    if (parsed[j] == "LABEL" && parsed[j+1] == label)
                    {
                        parsed[i+1] = (float)j;
                        break;
                    }
                }
            }
        }
    }

    // Parsing
    int labelCount = 0;
    int currentParsed = 0;
    object[] parsed;

    void Emit(string instr, object op)
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

    bool MatchNext(object tok)
    {
        return tok.Equals(PeekNext());
    }

    bool IsAtEnd()
    {
        return (currentLexed >= lexed.Length - 1) || (Peek() == null);
    }

    void Program()
    {
        while (!IsAtEnd() && !Statement());

        // final expression is evaluated
        Expression();

        // link up labels
        Link();
    }

    // Returns: Is final statement?
    [RecursiveMethod]
    bool Statement()
    {
        if (Peek().GetType() == typeof(string)) // potential keywords
        {
            string ident = (string)Peek();
            switch (ident) // keywords
            {
                case "set": case "let":
                    Assignment();
                    return false;
                case "if":
                    Conditional("end_"+labelCount++);
                    return false;
                case "while":
                    Loop(); 
                    return false;
                default:
                    return true;
            }
        }
        else
        {
            return true;
        }
    }

    [RecursiveMethod]
    void Conditional(string endLabel)
    {
        Eat(); // if
        Eat(); // (

        Expression(); // condition

        Eat(); // )
        Eat(); // {
        
        string falseLabel = "false_" + labelCount++;
        Emit("CONDJUMP", falseLabel);

        while (!IsAtEnd() && !Statement());

        Emit("JUMP", endLabel);
        Emit("LABEL", falseLabel);

        Eat(); // }

        if (Match("else"))
        {
            Eat();
            
            if (Match("if")) // else if
            {
                Conditional(endLabel);
            }
            else // else
            {
                Eat(); // {
                while (!IsAtEnd() && !Statement());
                Eat(); // }

                Emit("LABEL", endLabel);
            }
        }
        else
        {
            Emit("LABEL", endLabel);
        }
    }

    [RecursiveMethod]
    void Loop()
    {
        string startLabel = "start_" + labelCount++;
        string endLabel = "end_" + labelCount++;

        Eat(); // while
        Eat(); // (
        
        Emit("LABEL", startLabel);
        Expression(); // condition
        Emit("CONDJUMP", endLabel);

        Eat(); // )
        Eat(); // {

        while (!IsAtEnd() && !Statement());
        Emit("JUMP", startLabel);
        
        Eat(); // }

        Emit("LABEL", endLabel);
    }

    void Assignment()
    {
        Eat(); // set
        string ident = (string)Eat();
        if (Match('.'))
        {
            Eat();
            string swizzle = (string)Eat();
            ident += "." + swizzle;
        }
        Eat(); // =
        Expression();
        Eat(); // ;
        Emit("SETVAR", ident);
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
        Emit("CALL", ident);
    }

    [RecursiveMethod]
    void Expression()
    {
        Comparison();
    }

    [RecursiveMethod]
    void Comparison()
    {
        AddSub();

        while (true)
        {
            if ((Match('<') && MatchNext('=')) || (Match('>') && MatchNext('=')) || (Match('!') && MatchNext('=')) ||
                (Match('&') && MatchNext('&')) || (Match('|') && MatchNext('|')))
            {
                object tok1 = Eat();
                object tok2 = Eat();
                AddSub();
                Emit("BINOP", tok1 + "" + tok2);
                if (IsAtEnd()) return;
            }
            else if (Match('<') || Match('>') || Match('='))
            {
                object tok = Eat();
                AddSub();
                Emit("BINOP", tok.ToString());
                if (IsAtEnd()) return;
            }
            else
            {
                return;
            }
        }
    }

    [RecursiveMethod]
    void AddSub()
    {
        MulDiv();

        while (Match('+') || Match('-'))
        {
            object tok = Eat();
            MulDiv();
            Emit("BINOP", tok.ToString());
            if (IsAtEnd()) return;
        }
    }

    [RecursiveMethod]
    void MulDiv()
    {
        Term();

        while (Match('*') || Match('/'))
        {
            object tok = Eat();
            Term();
            Emit("BINOP", tok.ToString());
            if (IsAtEnd()) return;
        }
    }

    [RecursiveMethod]
    void Term()
    {
        if (IsAtEnd()) return;

        var type = Peek().GetType();
        if (type == typeof(string)) // identifier
        {
            if ('('.Equals(PeekNext())) // function call
            {
                // TODO: Codegen for constant vector constructors
                FuncCall();
            }
            else // var access
            {
                Emit("PUSHVAR", Eat());
            }
        }
        else if (type == typeof(float)) // literal
        {
            Emit("PUSHCONST", new Vector4((float)Eat(), 0, 0, 0));
        }
        else if (type == typeof(char))
        {
            if (Match('(')) // grouped
            {
                Eat();
                Expression();
                Eat();
            }
            else if (Match('+') || Match('-')) // unary op
            {
                var op = Eat();
                Term();
                Emit("UNOP", op.ToString());
            }
        }

        // Swizzling
        if (Match('.'))
        {
            Eat();

            string swizzle = (string)Eat();
            Vector4 res = Vector4.zero;
            for (int i = 0; i < 4; i++)
            {
                if (i >= swizzle.Length) break;

                switch (swizzle[i])
                {
                    case 'x': case 'r': res[i] = 1; break;
                    case 'y': case 'g': res[i] = 2; break;
                    case 'z': case 'b': res[i] = 3; break;
                    case 'w': case 'a': res[i] = 4; break;
                }
            }

            Emit("PUSHCONST", res);
            Emit("CALL", "swizzle");
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
        return (c >= '0' && c <= '9');
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
                while (i < input.Length && (IsNumeric(input[i]) || input[i] == '.')) // TODO: Only eat 1 period
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
