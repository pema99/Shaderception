
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
    // - Function calls (maybe)
    // - Explicit returns

    public InputField input;
    public Text output;

    public Material screenMat;
    public Material heightMat;

    public override void Interact()
    {
        // Lex
        currentLexed = 0;
        lexed = new object[4000];
        Debug.Log(input.text);
        Lex(input.text);

        // Parse / codegen
        currentLexed = 0;
        labelCount = 0;
        currentFunc = 0;
        funcIdents = new string[4000];
        funcParams = new string[4000][];
        funcIdents[0] = "global";
        currentParsed = 0;
        parsed = new object[4000][];
        parsed[0] = new object[4000];
        Block();

        // Linking
        regCount = 0;
        currentLinked = 0;
        linked = new object[4000];
        Link();
        output.text = "";
        for (int i = 0; i < linked.Length; i += 2)
        {
            if (linked[i] == null) break;
            output.text += (linked[i] + " " + linked[i + 1]) + "\n";
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
            case "uv":         return 30;
            case "xy":         return 31;
            case "time":       return 32;
            case "round":      return 33;                  
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
            case "==": return 7;
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
        Vector4 one = new Vector4(1, float.NaN, float.NaN, float.NaN);

        Vector4[] program = new Vector4[4000];

        for (int i = 0; i < linked.Length; i += 2)
        {
            if (linked[i] == null) program[i] = Vector4.zero;

            switch (linked[i])
            {
                case "PUSHCONST":
                    program[i] = one * 1;
                    program[i+1] = (Vector4)linked[i+1];
                    break;
                case "PUSHVAR":
                    program[i] = one * 2;
                    program[i+1] = one * float.Parse((string)linked[i+1]);
                    break;
                case "BINOP":
                    program[i] = one * 3;
                    program[i+1] = one * BinOpToIndex((string)linked[i+1]);
                    break;
                case "UNOP":
                    program[i] = one * 4;
                    program[i+1] = one * (float)((int)((string)linked[i+1])[0]);
                    break;
                case "CALL":
                    program[i] = one * 5;
                    program[i+1] = one * FuncIdentToIndex((string)linked[i+1]);
                    break;
                case "SETVAR":
                    program[i] = one * 6;
                    string[] parts = ((string)linked[i+1]).Split('.');
                    program[i+1] = one * float.Parse((string)linked[i+1]);
                    if (parts.Length > 1) // swizzle assignments
                    {
                        string swizzle = parts[1];
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
                    program[i+1] = one * (float)linked[i+1];
                    break;
                case "CONDJUMP":
                    program[i] = one * 8;
                    program[i+1] = one * (float)linked[i+1];
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
    int regCount = 0;
    string[] renameFrom;
    string[] renameTo;
    int currentLinked;
    object[] linked;

    [RecursiveMethod]
    void Inline(int id)
    {
        for (int i = 0; i < parsed[id].Length; i += 2)
        {
            if (parsed[id][i] == null) break;

            // User function, inline
            if (parsed[id][i] == "CALL" && FuncIdentToIndex((string)parsed[id][i+1]) == 0)
            {
                for (int j = 0; j < funcIdents.Length; j++) // find body of function to inline
                {
                    if (funcIdents[j] == null) break;

                    if (funcIdents[j].Equals(parsed[id][i+1]))
                    {
                        // store previous renaming table
                        string[] prevRenameFrom = null;
                        if (renameFrom != null)
                        {
                            prevRenameFrom = new string[renameFrom.Length];
                            System.Array.Copy(renameFrom, prevRenameFrom, renameFrom.Length);
                        }

                        // setup renaming table
                        renameFrom = funcParams[j];
                        renameTo = new string[renameFrom.Length];
                        for (int k = 0; k < renameTo.Length; k++)
                        {
                            if (renameFrom[k] == null) break;
                            renameTo[k] = "_reg" + regCount++;
                        }

                        // recursively inline function
                        Inline(j);

                        // restore renaming table
                        renameFrom = prevRenameFrom;
                        break;
                    }
                }
            }
            else
            {
                // Rename variables if a mapping table is available
                if (parsed[id][i] == "PUSHVAR" || parsed[id][i] == "SETVAR")
                {
                    string ident = (string)parsed[id][i+1];
                    if (renameFrom != null)
                    {
                        for (int j = 0; j < renameFrom.Length; j++)
                        {
                            if (ident == renameFrom[j])
                            {
                                parsed[id][i+1] = renameTo[j];
                                break;
                            }
                        }
                    }
                }
                linked[currentLinked++] = parsed[id][i];
                linked[currentLinked++] = parsed[id][i+1];
            }
        }
    }

    void Link()
    {
        renameFrom = null;
        renameTo = null;

        // Inlining, start with global scope
        Inline(0);

        // Jump location linking
        for (int i = 0; i < linked.Length; i += 2)
        {
            if (linked[i] == null) break;

            if (linked[i] == "JUMP" || linked[i] == "CONDJUMP")
            {
                string label = (string)linked[i+1];
                for (int j = 0; j < linked.Length; j++)
                {
                    if (linked[j] == null) break;

                    if (linked[j] == "LABEL" && linked[j+1] == label)
                    {
                        linked[i+1] = (float)j;
                        break;
                    }
                }
            }
        }

        // Register allocation
        bool[] alloced = new bool[linked.Length];
        int regAlloc = 0;
        for (int i = 0; i < linked.Length; i += 2)
        {
            if (linked[i] == null) break;

            if (linked[i] == "PUSHVAR" || linked[i] == "SETVAR")
            {
                if (alloced[i]) // don't allocate registers multiple times
                    continue;

                string reg = (regAlloc++).ToString();
                string[] a = linked[i+1].ToString().Split('.');

                for (int j = 0; j < linked.Length; j += 2)
                {
                    if (linked[j] == null) break;

                    string[] b = linked[j+1].ToString().Split('.');

                    if ((linked[j] == "PUSHVAR" || linked[j] == "SETVAR") && a[0] == b[0])
                    {
                        if (b.Length > 1) // handle swizzle
                        {
                            linked[j+1] = reg + b[1];
                        }
                        else
                        {
                            linked[j+1] = reg;
                        }
                        alloced[j] = true;
                    }
                }
            }
        }
    }

    // Parsing
    int labelCount = 0;
    int currentFunc = 0;
    string[] funcIdents;
    string[][] funcParams;
    int prevParsed = 0;
    int currentParsed = 0;
    object[][] parsed;

    void SwitchToFunction(string ident, string[] parameters)
    {
        for (int i = 0; i < funcIdents.Length; i++)
        {
            if (string.IsNullOrEmpty(funcIdents[i]))
            {
                funcIdents[i] = ident;
                funcParams[i] = parameters;
                parsed[i] = new object[4000];
            }

            if (funcIdents[i] == ident)
            {
                if (currentFunc == 0)
                {
                    prevParsed = currentParsed;
                }
                currentParsed = 0;
                currentFunc = i;
                break;
            }
        }
    }

    void SwitchToGlobal()
    {
        currentParsed = prevParsed;
        currentFunc = 0;
    }

    void Emit(string instr, object op)
    {
        parsed[currentFunc][currentParsed++] = instr;
        parsed[currentFunc][currentParsed++] = op;
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

    [RecursiveMethod]
    void Block()
    {
        while (!IsAtEnd() && !Statement());

        // final expression is evaluated
        Expression();
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
                case "fun":
                    FuncDef();
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
    void FuncDef()
    {
        string[] parameters = new string[16];

        Eat(); // fun
        string ident = (string)Eat();
        Eat(); // (

        int arity = 0;
        if (Peek().GetType() == typeof(string)) // has parameters
        {
            parameters[arity++] = (string)Eat();
            while (Match(','))
            {
                Eat(); // ,
                parameters[arity++] = (string)Eat();
            }
        }

        Eat(); // )
        Eat(); // {

        // emit body into correct store
        SwitchToFunction(ident, parameters);

        // pop parameters from stack in reverse order and put in registers
        for (int i = 0; i < arity; i++)
        {
            Emit("SETVAR", parameters[arity-1-i]);
        }

        // body
        Block();

        // restore global store
        SwitchToGlobal();

        Eat(); // }
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

        Block();

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
                Block();
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

        Block();
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
                (Match('&') && MatchNext('&')) || (Match('|') && MatchNext('|')) || (Match('=') && MatchNext('=')))
            {
                object tok1 = Eat();
                object tok2 = Eat();
                AddSub();
                Emit("BINOP", tok1 + "" + tok2);
                if (IsAtEnd()) return;
            }
            else if (Match('<') || Match('>'))
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
            Emit("PUSHCONST", new Vector4((float)Eat(), float.NaN, float.NaN, float.NaN));
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
