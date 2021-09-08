
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using UnityEngine.UI;

public class Compiler : UdonSharpBehaviour
{
    // TODO:
    // Problems:
    //     Error handling!
    //     Implicit casts for user defined functions
    //     Lerp with scalar broken
    //     Don't inline forever
    //     Inlining bug: https://pastebin.com/ygMQ8VSn
    // Add:
    //     Audiolink
    //     Arbitrary writes with geom
    //     Indirect jump
    // Maybe:
    //     Early returns
    //     Non-inlined functions. Actual callstack?
    //     C-style defines

    public InputField input;
    public Text output;
    public Material screenMat;

    // Runtime
    void Start()
    {
        Compile();
    }

    Vector4 inputButton;
    Vector4 inputAxis;
    public override void InputUse(bool isPressed, UdonInputEventArgs args)
    {
        inputButton.x = (isPressed ? 1 : 0);
    }

    public override void InputJump(bool isPressed, UdonInputEventArgs args)
    {
        inputButton.y = (isPressed ? 1 : 0);
    }

    public override void InputGrab(bool isPressed, UdonInputEventArgs args)
    {
        inputButton.z = (isPressed ? 1 : 0);
    }

    public override void InputDrop(bool isPressed, UdonInputEventArgs args)
    {
        inputButton.w = (isPressed ? 1 : 0);
    }

    public override void InputMoveHorizontal(float val, UdonInputEventArgs args)
    {
        inputAxis.x = val;
    }

    public override void InputMoveVertical(float val, UdonInputEventArgs args)
    {
        inputAxis.y = val;
    }

    public override void InputLookHorizontal(float val, UdonInputEventArgs args)
    {
        inputAxis.z = val;
    }

    public override void InputLookVertical(float val, UdonInputEventArgs args)
    {
        inputAxis.w = val;
    }

    void Update()
    {
        screenMat.SetVector("_InputButton", inputButton);
        screenMat.SetVector("_InputAxis", inputAxis);
    }

    // Compiler
    public void Compile()
    {
        error = null;

        // Lex
        currentLexed = 0;
        lexed = new object[4092];
        Lex(input.text);
        if (HasError())
        {
            output.text = error;
            return;
        }

        // Parse / codegen
        currentLexed = 0;
        labelCount = 0;
        currentFunc = 0;
        funcIdents = new string[4092];
        funcParams = new string[4092][];
        funcIdents[0] = "global";
        currentParsed = 0;
        parsed = new object[4092][];
        parsed[0] = new object[4092];
        Block();
        if (HasError())
        {
            output.text = error;
            return;
        }

        // Linking
        inlineCount = 0;
        regCount = 0;
        currentGlobals = 0;
        globals = new string[4092];
        currentLinked = 0;
        linked = new object[4092];
        currentLabels = 0;
        labels = new object[4092];
        Link();
        if (HasError())
        {
            output.text = error;
            return;
        }
        output.text = "";
        for (int i = 0; i < linked.Length; i += 2)
        {
            if (linked[i] == null) break;
            output.text += (linked[i] + " " + linked[i + 1]) + "\n";
        }

        // Write to material
        WriteProgramToMaterial(screenMat);
    }

    // Error handling
    string error = null;

    void Error(string text)
    {
        if (error == null)
        {
            error = "Error: " + text;
        }
    }

    bool HasError()
    {
        return error != null;
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
            case "dot":        return 34;
            case "cross":      return 35;
            case "distance":   return 36;
            case "normalize":  return 37;
            case "length":     return 38;
            case "reflect":    return 39;
            case "refract":    return 40;
            case "self":       return 41;
            case "resolution": return 42;
            case "button":     return 43;
            case "axis":       return 44;
            case "camera":     return 45;
            case "deltatime":  return 46;
            case "video":      return 47;
            case "all":        return 48;
            case "any":        return 49;
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

        Vector4[] program = new Vector4[4092];

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
                    program[i+1] = one * float.Parse(parts[0]);
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

        // split binary to put into aliased cbuffer
        Vector4[][] split = new Vector4[4][]
        {
            new Vector4[1023],
            new Vector4[1023],
            new Vector4[1023],
            new Vector4[1023]
        };
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 1023; j++)
            {
                split[i][j] = program[i * 1023 + j];
            }
        }
        mat.SetVectorArray("_Program0", split[0]);
        mat.SetVectorArray("_Program1", split[1]);
        mat.SetVectorArray("_Program2", split[2]);
        mat.SetVectorArray("_Program3", split[3]);
    }

    // Linking
    int inlineCount = 0;
    int regCount = 0;
    string[] renameFrom;
    string[] renameTo;
    int currentGlobals;
    string[] globals;
    int currentLinked;
    object[] linked;
    int currentLabels = 0;
    object[] labels;

    [RecursiveMethod]
    void Inline(int id)
    {
        int currentInline = inlineCount++;

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
                        string[] prevRenameTo = null;
                        if (renameFrom != null || renameTo != null)
                        {
                            prevRenameFrom = new string[renameFrom.Length];
                            System.Array.Copy(renameFrom, prevRenameFrom, renameFrom.Length);
                            prevRenameTo = new string[renameTo.Length];
                            System.Array.Copy(renameTo, prevRenameTo, renameTo.Length);
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
                        renameTo = prevRenameTo;
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

                    // When in global scope, keep track of global variables
                    string[] local = ((string)parsed[id][i+1]).Split('.');
                    bool isGlobal = false;
                    if (id == 0)
                    {
                        globals[currentGlobals++] = local[0];
                        isGlobal = true;
                    }
                    // For local vars, prepend function name to avoid ambiguity in different scopes
                    else
                    {
                        for (int j = 0; j < globals.Length; j++)
                        {
                            if (globals[j] == null) break;
                            if (globals[j] == local[0])
                            {
                                isGlobal = true;
                                break;
                            }
                        }
                    }

                    if (isGlobal)
                    {
                        linked[currentLinked++] = parsed[id][i];
                        linked[currentLinked++] = parsed[id][i+1];
                    }
                    else
                    {
                        linked[currentLinked++] = parsed[id][i];
                        linked[currentLinked++] = funcIdents[id] + "_" + parsed[id][i+1];
                    }
                }
                // Dont add labels, rename labels per inline
                else if (parsed[id][i] == "LABEL")
                {
                    labels[currentLabels++] = parsed[id][i+1] + "_" + currentInline;
                    labels[currentLabels++] = currentLinked;
                }
                // Rename labes per inline
                else if (parsed[id][i] == "JUMP" || parsed[id][i] == "CONDJUMP")
                {
                    linked[currentLinked++] = parsed[id][i];
                    linked[currentLinked++] = parsed[id][i+1] + "_" + currentInline;
                }
                // All other instructions
                else
                {
                    linked[currentLinked++] = parsed[id][i];
                    linked[currentLinked++] = parsed[id][i+1];
                }
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
                for (int j = 0; j < labels.Length; j += 2)
                {
                    if (label.Equals(labels[j]))
                    {
                        linked[i+1] = (float)labels[j+1];
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
                            linked[j+1] = reg + "." + b[1];
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

    int GetFuncArity(string ident)
    {
        switch (FuncIdentToIndex(ident))
        {
            case 1:  return 1;
            case 2:  return 1;
            case 3:  return 1;
            case 4:  return 1;
            case 5:  return 1;
            case 6:  return 1;
            case 7:  return 1;
            case 8:  return 1;
            case 9:  return 2;
            case 10: return 1;
            case 11: return 1;
            case 12: return 1;
            case 13: return 1;
            case 14: return 1;
            case 15: return 1;
            case 16: return 1;
            case 17: return 1;
            case 18: return 1;
            case 19: return 2;
            case 20: return 2;
            case 21: return 2;
            case 22: return 3;
            case 23: return 3;
            case 24: return 2;
            case 25: return 3;
            case 26: return 2;
            case 27: return 3;
            case 28: return 4;
            case 29: return 2;
            case 30: return 0;
            case 31: return 0;
            case 32: return 0;
            case 33: return 1;
            case 34: return 2;
            case 35: return 2;
            case 36: return 2;
            case 37: return 1;
            case 38: return 1;
            case 39: return 2;
            case 40: return 3;
            case 41: return 1;
            case 42: return 0;
            case 43: return 0;
            case 44: return 0;
            case 45: return 1;
            case 46: return 0;
            case 47: return 1;
            case 48: return 1;
            case 49: return 1;
            default:
                for (int i = 0; i < funcIdents.Length; i++)
                {
                    if (funcIdents[i] == null) break;

                    if (funcIdents[i] == ident)
                    {
                        int count = 0;
                        for (int j = 0; j < funcParams[i].Length; j++)
                        {
                            if (funcParams[i][j] == null) break;

                            count++;
                        }
                        return count;
                    }
                }
                return -1;
        }
    }

    void SwitchToFunction(string ident, string[] parameters)
    {
        for (int i = 0; i < funcIdents.Length; i++)
        {
            if (string.IsNullOrEmpty(funcIdents[i]))
            {
                funcIdents[i] = ident;
                funcParams[i] = parameters;
                parsed[i] = new object[4092];
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
        if (currentLexed < lexed.Length)
        {
            return lexed[currentLexed];
        }
        else
        {
            return false;
        }
    }

    object PeekNext()
    {
        if (currentLexed + 1 < lexed.Length)
        {
            return lexed[currentLexed + 1];
        }
        else
        {
            return false;
        }
    }

    object PeekTo(int i)
    {
        if (currentLexed + i < lexed.Length)
        {
            return lexed[currentLexed + i];
        }
        else
        {
            return false;
        }
    }

    object Advance()
    {
        if (currentLexed < lexed.Length)
        {
            return lexed[currentLexed++];
        }
        else
        {
            return false;
        }
    }

    object Eat(object tok)
    {
        if (Peek() == null)
        {
            Error("Expected token '" + tok + "', but reached end of program.");
            return false;
        }
        else if (!Match(tok))
        {
            Error("Expected token '" + tok + "', found token: " + Peek());
            return false;
        }
        else
        {
            return Advance();
        }
    }

    string EatIdent()
    {
        if (Peek() == null)
        {
            Error("Expected identifier, but reached end of program.");
            return "";
        }
        else if (Peek().GetType() != typeof(string))
        {
            Error("Expected identifier, found token: " + Peek());
            return "";
        }
        else
        {
            return (string)Advance();
        }
    }

    float EatFloat()
    {
        if (Peek() == null)
        {
            Error("Expected float literal, but reached end of program.");
            return 0;
        }
        else if (Peek().GetType() != typeof(float))
        {
            Error("Expected float literal, found token: " + Peek());
            return 0;
        }
        else
        {
            return (float)Advance();
        }
    }

    bool Match(object tok)
    {
        return tok.Equals(Peek());
    }

    bool MatchNext(object tok)
    {
        return tok.Equals(PeekNext());
    }

    bool MatchTo(int i, object tok)
    {
        return tok.Equals(PeekTo(i));
    }

    bool IsAtEnd()
    {
        return (currentLexed >= lexed.Length - 1) || (Peek() == null) || HasError();
    }

    [RecursiveMethod]
    void Block()
    {
        while (!IsAtEnd() && !Statement());

        // optional return statement
        bool hasReturn = Match("return");
        if (hasReturn) Advance();

        // final expression is evaluated
        Expression();

        // optional semicolon for return
        if (hasReturn) Eat(';');

        if (!IsAtEnd() && !Match('}')) Error("End of block reached, but there is more code.");
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
                    Eat(';');
                    return false;
                case "if":
                    Conditional("end_"+labelCount++);
                    return false;
                case "while":
                    WhileLoop(); 
                    return false;
                case "for":
                    ForLoop();
                    return false;
                case "fun":
                    FuncDef();
                    return false;
                default:
                    if (IsAssignment())
                    {
                        Assignment();
                        Eat(';');
                        return false;
                    }
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

        Eat("fun");
        string ident = EatIdent();
        Eat('(');

        int arity = 0;
        if (Peek().GetType() == typeof(string)) // has parameters
        {
            parameters[arity++] = EatIdent();
            while (Match(','))
            {
                Eat(',');
                parameters[arity++] = EatIdent();
            }
        }

        Eat(')');
        Eat('{');

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

        Eat('}');
    }

    [RecursiveMethod]
    void Conditional(string endLabel)
    {
        Eat("if");
        Eat('(');

        Expression(); // condition

        Eat(')');
        Eat('{');
        
        string falseLabel = "false_" + labelCount++;
        Emit("CONDJUMP", falseLabel);

        Block();

        Emit("JUMP", endLabel);
        Emit("LABEL", falseLabel);

        Eat('}');

        if (Match("else"))
        {
            Eat("else");
            
            if (Match("if")) // else if
            {
                Conditional(endLabel);
            }
            else // else
            {
                Eat('{');
                Block();
                Eat('}');

                Emit("LABEL", endLabel);
            }
        }
        else
        {
            Emit("LABEL", endLabel);
        }
    }

    [RecursiveMethod]
    void WhileLoop()
    {
        string startLabel = "start_" + labelCount++;
        string endLabel = "end_" + labelCount++;

        Eat("while");
        Eat('(');
        
        Emit("LABEL", startLabel);
        Expression(); // condition
        Emit("CONDJUMP", endLabel);

        Eat(')');
        Eat('{');

        Block();
        Emit("JUMP", startLabel);
        
        Eat('}');

        Emit("LABEL", endLabel);
    }

    [RecursiveMethod]
    void ForLoop()
    {
        string checkLabel = "check_" + labelCount++;
        string loopLabel = "loop_" + labelCount++;
        string incrementLabel = "incr_" + labelCount++;
        string endLabel = "end_" + labelCount++;

        Eat("for");
        Eat('(');

        Assignment(); // init
        Eat(';');

        Emit("LABEL", checkLabel);
        Expression(); // condition
        Eat(';');

        Emit("CONDJUMP", endLabel);
        Emit("JUMP", loopLabel);
        Emit("LABEL", incrementLabel);

        Assignment(); // increment

        Eat(')');

        Emit("JUMP", checkLabel);
        Emit("LABEL", loopLabel);

        Eat('{');

        Block();
        Emit("JUMP", incrementLabel);
        
        Eat('}');

        Emit("LABEL", endLabel);
    }

    bool IsAssignment()
    {
        if (Match("let") || Match("set")) return true; // normal assignments
        if (Peek().GetType() == typeof(string))
        {
            // Handle swizzle
            int offset = 1;
            if (MatchTo(1, '.') && PeekTo(2).GetType() == typeof(string))
            {
                offset += 2;
            }

            // Assignment types
            if (MatchTo(offset, '=') && !MatchTo(offset+1, '=')) return true;
            if (MatchTo(offset, '+') &&  MatchTo(offset+1, '=')) return true;
            if (MatchTo(offset, '-') &&  MatchTo(offset+1, '=')) return true;
            if (MatchTo(offset, '*') &&  MatchTo(offset+1, '=')) return true;
            if (MatchTo(offset, '/') &&  MatchTo(offset+1, '=')) return true;
            if (MatchTo(offset, '+') &&  MatchTo(offset+1, '+')) return true;
            if (MatchTo(offset, '-') &&  MatchTo(offset+1, '-')) return true;
        }
        return false;
    }

    void Assignment()
    {
        if (Match("let") || Match("set"))
        {
            Advance();
        }

        string ident = EatIdent();
        string identFirst = ident;
        if (Match('.')) // handle swizzle
        {
            Eat('.');
            string swizzle = EatIdent();
            ident += "." + swizzle;
        }
        if (Match('=')) // normal assignment
        {
            Eat('=');
            Expression();
            Emit("SETVAR", ident);
        }
        else if (MatchNext('=') && (Match('+') || Match('-') || Match('*') || Match('/'))) // math assignment
        {
            Emit("PUSHVAR", identFirst);

            string op = Advance().ToString();
            Eat('=');
            Expression();

            Emit("BINOP", op);
            Emit("SETVAR", ident);
        }
        else if ((Match('+') && MatchNext('+')) || (Match('-') && MatchNext('-'))) // ++, --
        {
            Emit("PUSHVAR", identFirst);

            string op = Advance().ToString();
            Advance();

            Emit("PUSHCONST", new Vector4(1f, float.NaN, float.NaN, float.NaN));
            Emit("BINOP", op);
            Emit("SETVAR", ident);
        }
        else
        {
            Error("Unknown type of assignment.");
        }
    }

    [RecursiveMethod]
    void FuncCall()
    {
        string ident = EatIdent(); // ident
        Eat('(');
        if (!Match(')'))
        {
            int arity = 1;
            Expression();
            while (Match(','))
            {
                Eat(',');
                Expression();
                arity++;
            }

            // Check arity with expectations
            int expected = GetFuncArity(ident);
            if (expected == -1)
            {
                Error($"Use of undefined function {ident}.");
            }
            else if (expected != arity)
            {
                Error($"Incorrect number of parameters to function {ident}, expected {expected}, got {arity}.");
            }
        }
        Eat(')');
        Emit("CALL", ident);
    }

    [RecursiveMethod]
    void Expression()
    {
        BoolOp();
    }

    [RecursiveMethod]
    void BoolOp()
    {
        Comparison();

        while (true)
        {
            if ((Match('&') && MatchNext('&')) || (Match('|') && MatchNext('|')))
            {
                object tok1 = Advance();
                object tok2 = Advance();
                Comparison();
                Emit("BINOP", tok1 + "" + tok2);
                if (IsAtEnd()) return;
            }
            else
            {
                return;
            }
        }
    }

    [RecursiveMethod]
    void Comparison()
    {
        AddSub();

        while (true)
        {
            if ((Match('<') && MatchNext('=')) || (Match('>') && MatchNext('=')) ||
                (Match('!') && MatchNext('=')) || (Match('=') && MatchNext('=')))
            {
                object tok1 = Advance();
                object tok2 = Advance();
                AddSub();
                Emit("BINOP", tok1 + "" + tok2);
                if (IsAtEnd()) return;
            }
            else if (Match('<') || Match('>'))
            {
                object tok = Advance();
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
            object tok = Advance();
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
            object tok = Advance();
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
                FuncCall();
            }
            else // var access
            {
                Emit("PUSHVAR", EatIdent());
            }
        }
        else if (type == typeof(float)) // literal
        {
            Emit("PUSHCONST", new Vector4(EatFloat(), float.NaN, float.NaN, float.NaN));
        }
        else if (type == typeof(char))
        {
            if (Match('(')) // grouped
            {
                Eat('(');
                Expression();
                Eat(')');
            }
            else if (Match('+') || Match('-') || Match('!')) // unary op
            {
                var op = Advance();
                Term();
                Emit("UNOP", op.ToString());
            }
        }

        // Swizzling
        if (Match('.'))
        {
            Eat('.');

            string swizzle = EatIdent();
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
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c == '_');
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
                else
                    Error("Failed to parse float literal: " + ident);
            }

            // whitespace
            else if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                i++;
            }

            // comments
            else if (c == '/' && i+1 < input.Length && input[i+1] == '/')
            {
                while (i < input.Length && input[i] != '\n')
                {
                    i++;
                }
            }

            // single token
            else
            {
                lexed[currentLexed++] = c;
                i++;
            }
        }
    }
}
