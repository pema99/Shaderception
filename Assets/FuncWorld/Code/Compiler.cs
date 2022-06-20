
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class Compiler : UdonSharpBehaviour
{
    public InputField input;
    public Text output;
    public UdonInterpreter interpreter;

    public void Start()
    {
        string code =
".data_start\n" +
"    __refl_const_intnl_udonTypeID: %SystemInt64, null\n" +
"    __refl_const_intnl_udonTypeName: %SystemString, null\n" +
"    __0_b_Single: %SystemSingle, null\n" +
"    __0_a_Single: %SystemSingle, null\n" +
"    __1_const_intnl_SystemInt32: %SystemInt32, null\n" +
"    __0_const_intnl_SystemInt32: %SystemInt32, null\n" +
"    __0_const_intnl_SystemUInt32: %SystemUInt32, null\n" +
"    __0_intnl_SystemSingle: %SystemSingle, null\n" +
"    __0_intnl_returnTarget_UInt32: %SystemUInt32, null\n" +
"\n" +
".data_end\n" +
"\n" +
"        \n" +
"         #  using UdonSharp;\n" +
"        \n" +
"         #  using UnityEngine;\n" +
"        \n" +
"         #  using VRC.SDKBase;\n" +
"        \n" +
"         #  using VRC.Udon;\n" +
"        \n" +
"         #  public class test : UdonSharpBehaviour\n" +
".code_start\n" +
"        \n" +
"         #  void Start()\n" +
"    .export _start\n" +
"        \n" +
"    _start:\n" +
"        \n" +
"        PUSH, __0_const_intnl_SystemUInt32\n" +
"        \n" +
"         #  {\n" +
"        \n" +
"         #  float a = 1337;\n" +
"        PUSH, __0_const_intnl_SystemInt32\n" +
"        PUSH, __0_a_Single\n" +
"        EXTERN, \"SystemConvert.__ToSingle__SystemInt32__SystemSingle\"\n" +
"        \n" +
"         #  float b = 420;\n" +
"        PUSH, __1_const_intnl_SystemInt32\n" +
"        PUSH, __0_b_Single\n" +
"        EXTERN, \"SystemConvert.__ToSingle__SystemInt32__SystemSingle\"\n" +
"        \n" +
"         #  Debug.Log(a + b);\n" +
"        PUSH, __0_a_Single\n" +
"        PUSH, __0_b_Single\n" +
"        PUSH, __0_intnl_SystemSingle\n" +
"        EXTERN, \"SystemSingle.__op_Subtraction__SystemSingle_SystemSingle__SystemSingle\"\n" +
"        PUSH, __0_intnl_SystemSingle\n" +
"        EXTERN, \"UnityEngineDebug.__Log__SystemObject__SystemVoid\"\n" +
"        PUSH, __0_intnl_returnTarget_UInt32 # Function epilogue\n" +
"        COPY\n" +
"        JUMP_INDIRECT, __0_intnl_returnTarget_UInt32\n" +
"        \n" +
".code_end";
        input.text = code;
        Compile();
    }

    public void Step()
    {
        interpreter.Step();
    }

    // Compiler
    public void Compile()
    {
        interpreter.code = input.text;
        interpreter.Init();

        return; // TODO
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
        currentConsts = 0;
        consts = new object[4092];
        Inline(0);
        if (HasError())
        {
            output.text = error;
            return;
        }

        string data = "";
        for (int i = 0; i < consts.Length; i ++)
        {
            if (consts[i] == null) break;

            data += $"__const_{i}: %SystemSingle, null #{consts[i]}\n";
        }

        string code = "";
        for (int i = 0; i < linked.Length; i += 2)
        {
            if (linked[i] == null) break;
            //output.text += (linked[i] + " " + linked[i + 1]) + "\n";

            switch (linked[i])
            {
                case "PUSHCONST":
                    code += $"PUSH, __const_{linked[i+1]}\n";
                    //program[i] = one * 1;
                    //program[i+1] = (Vector4)linked[i+1];
                    break;
                case "PUSHVAR":
                    code += $"PUSH, {linked[i+1]}\n";
                    //program[i] = one * 2;
                    //program[i+1] = one * float.Parse((string)linked[i+1]);
                    break;
                case "BINOP":
                    code += "PUSH, __acc_SystemSingle\n";
                    code += $"EXTERN, \"{BinOpToUdon((string)linked[i+1])}\"\n";
                    code += "PUSH, __acc_SystemSingle\n";
                    //program[i] = one * 3;
                    //program[i+1] = one * BinOpToIndex((string)linked[i+1]);
                    break;
                case "UNOP":
                    code += "PUSH, __acc_SystemSingle\n";
                    code += $"EXTERN, \"{UnOpToUdon((string)linked[i+1])}\"\n";
                    code += "PUSH, __acc_SystemSingle\n";
                    //program[i] = one * 4;
                    //program[i+1] = one * (float)((int)((string)linked[i+1])[0]);
                    break;
                case "CALL":
                    code += "PUSH, __acc_SystemSingle\n";
                    code += $"EXTERN, \"{FuncIdentToUdon((string)linked[i+1])}\"\n";
                    code += "PUSH, __acc_SystemSingle\n";
                    //program[i] = one * 5;
                    //program[i+1] = one * FuncIdentToIndex((string)linked[i+1]);
                    break;
                case "SETVAR":
                    code += $"PUSH, {linked[i+1]}\n";
                    code += $"COPY\n";
                    //program[i] = one * 6;
                    //string[] parts = ((string)linked[i+1]).Split('.');
                    //program[i+1] = one * float.Parse(parts[0]);
                    /*if (parts.Length > 1) // swizzle assignments
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
                    }*/
                    break;
                case "JUMP":
                    code += $"JUMP, {linked[i+1]}\n";
                    //program[i] = one * 7;
                    //program[i+1] = one * (float)linked[i+1];
                    break;
                case "CONDJUMP":
                    code += $"JUMP_IF_FALSE, {linked[i+1]}\n";
                    //program[i] = one * 8;
                    //program[i+1] = one * (float)linked[i+1];
                    break;
                case "LABEL":
                    code += $"{linked[i+1]}:\n";
                    //program[i] = one * 9;
                    //program[i+1] = Vector4.zero;
                    break;
            }
        }

        output.text = @".data_start
__acc_SystemSingle: %SystemSingle, null
###DATA
.data_end

.code_start

.export _update

_update:
###CODE
.code_end".Replace("###CODE", code).Replace("###DATA", data);

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
    string FuncIdentToUdon(string ident)
    {
        return ident; // TODO
    }

    string BinOpToUdon(string op)
    {
        switch (op)
        {
            case "+":  return "SystemSingle.__op_Addition__SystemSingle_SystemSingle__SystemSingle";
            case "-":  return "SystemSingle.__op_Subtraction__SystemSingle_SystemSingle__SystemSingle";
            case "*":  return "SystemSingle.__op_Multiplication__SystemSingle_SystemSingle__SystemSingle";
            case "/":  return "SystemSingle.__op_Division__SystemSingle_SystemSingle__SystemSingle";
            case "<":  return "SystemSingle.__op_LessThan__SystemSingle_SystemSingle__SystemBoolean";
            case ">":  return "SystemSingle.__op_GreaterThan__SystemSingle_SystemSingle__SystemBoolean";
            case "==": return "SystemSingle.__op_Equality__SystemSingle_SystemSingle__SystemBoolean";
            case "<=": return "SystemSingle.__op_LessThanOrEqual__SystemSingle_SystemSingle__SystemBoolean";
            case ">=": return "SystemSingle.__op_GreaterThanOrEqual__SystemSingle_SystemSingle__SystemBoolean";
            case "!=": return "SystemSingle.__op_Inequality__SystemSingle_SystemSingle__SystemBoolean";
            case "&&": return "TODO: &&";
            case "||": return "TODO: ||";
            default:   return "TODO: " + op;
        }
    }

    string UnOpToUdon(string op)
    {
        switch (op)
        {
            case "-": return "SystemSingle.__op_UnaryMinus__SystemSingle__SystemSingle";
            case "!": return "TODO: !";
            default:  return "TODO: " + op;
        }
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
    int currentConsts = 0;
    object[] consts;

    [RecursiveMethod]
    void Inline(int id)
    {
        int currentInline = inlineCount++;

        for (int i = 0; i < parsed[id].Length; i += 2)
        {
            if (parsed[id][i] == null) break;

            // User function, inline
            if (parsed[id][i].Equals("CALL")/* && FuncIdentToIndex((string)parsed[id][i+1]) == 0*/)
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
                if (parsed[id][i].Equals("PUSHVAR") || parsed[id][i].Equals("SETVAR"))
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
                // Rename labes per inline
                else if (parsed[id][i].Equals("JUMP") || parsed[id][i].Equals("CONDJUMP") || parsed[id][i].Equals("LABEL"))
                {
                    linked[currentLinked++] = parsed[id][i];
                    linked[currentLinked++] = parsed[id][i+1] + "_" + currentInline;
                }
                // Gather constants
                else if (parsed[id][i].Equals("PUSHCONST"))
                {
                    linked[currentLinked++] = parsed[id][i];
                    linked[currentLinked++] = currentConsts;
                    consts[currentConsts++] = parsed[id][i+1];
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
        /*switch (FuncIdentToIndex(ident))
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

            default:*/
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
        //}
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
            // Handle field access
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
