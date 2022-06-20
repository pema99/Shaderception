
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class UdonInterpreter : UdonSharpBehaviour
{
    private string[] asm;
    private int pc = 0;
    
    private object[] heap;
    private int[] stack;
    private int sp = 0;
    private int[] addrLUT;
    private object[] labels;

    public string code;
    public UdonBehaviour externInvoker;
    public Text output;

    public void Init()
    {
        sp = 0;
        pc = 0;
        heap = new object[65536];
        stack = new int[65536];
        addrLUT = new int[65536*8];

        var codeParts = code.Split(new string[] { ".code_start" }, System.StringSplitOptions.None);
        var rawData = codeParts[0].Split(new string[] { "\n" }, System.StringSplitOptions.None);
        var rawCode = codeParts[1].Split(new string[] { "\n" }, System.StringSplitOptions.None);

        // Walk data, find variables
        var variables = new string[65535];
        var currVariables = 0;
        for (int i = 0; i < rawData.Length; i++)
        {
            string line = rawData[i].Trim();
            int idx0 = line.IndexOf("#");
            if (idx0 != -1)
                line = line.Remove(idx0);
            
            if (line.Contains(":"))
            {
                variables[currVariables++] = line.Split(':')[0].Trim();
                //Debug.Log("VARIABLE: "+ line.Split(':')[0].Trim());
            }
        }

        // Walk code, find labels and addresses
        var clean = new string[rawCode.Length];
        labels = new object[65536];
        int currAddr = 0;
        int cleanIdx = 0;
        int currLabel = 0;
        for (int i = 0; i < rawCode.Length; i++)
        {
            // Parse
            string line = rawCode[i].Trim();
            int idx0 = line.IndexOf("#");
            if (idx0 != -1)
                line = line.Remove(idx0);
            
            if (line == "") continue;

            // Replace variables with indices where applicable
            int idx1 = line.IndexOf(",")+1;
            string op = "";
            if (idx1 != -1)
                op = line.Substring(idx1).Trim();
            if (op != "")
            {
                //Debug.Log("TRYING TO REPLACE: "+ op);
                for (int j = 0; j < currVariables; j++)
                {
                    if (variables[j].Equals(op))
                    {
                        line = line.Replace(op, j.ToString());
                    }
                }
            }
            
            // Account for address offsets
            int size = -1;
            if      (line.StartsWith("PUSH"))          { size = 8; }
            else if (line.StartsWith("POP"))           { size = 4; }
            else if (line.StartsWith("JUMP_IF_FALSE")) { size = 8; }
            else if (line.StartsWith("JUMP"))          { size = 8; }
            else if (line.StartsWith("EXTERN"))        { size = 8; }
            else if (line.StartsWith("JUMP_INDIRECT")) { size = 8; }
            else if (line.StartsWith("COPY"))          { size = 4; }
            else if (line.StartsWith("NOP"))           { size = 4; }
            else if (line.StartsWith("ANNOTATION"))    { size = 4; }

            // If there is an instruction, add it, and keep track of offsets
            if (size != -1)
            {
                addrLUT[currAddr] = cleanIdx;
                clean[cleanIdx] = line;
                currAddr += size;
                cleanIdx++;
            }

            // If there is a label, store it
            else if (line.Contains(":"))
            {
                labels[currLabel++] = line.Split(':')[0].Trim();
                labels[currLabel++] = currAddr;
                //Debug.Log("LABEL: "+ line.Split(':')[0].Trim());
            }
        }
        asm = new string[cleanIdx];
        System.Array.Copy(clean, asm, cleanIdx); 
    }

    void Push(int o)
    {
        stack[sp++] = o;
    }

    int Pop()
    {
        return stack[--sp];
    }

    void Jump(int addr)
    {
        pc = addrLUT[addr];
    }

    int ParseAddress(string addr)
    {
        int res = 0;
        if (int.TryParse(addr, out res))
        {
            return res;
        }

        // TODO: Hex string

        // TODO: Variables

        for (int i = 0; i < labels.Length; i += 2)
        {
            if (labels[i] == null) break;

            if (labels[i].Equals(addr))
            {
                return (int)labels[i+1];
            }
        }

        return -1;
    }

    public void Step()
    {
        //TODO: Set pc

        if (pc > asm.Length - 1) return;

        string line = asm[pc++];

        int idx1 = line.IndexOf(",")+1;
        string op = "";
        if (idx1 != -1)
            op = line.Substring(idx1).Trim();
        
        if (line.StartsWith("PUSH"))
        {
            Push(ParseAddress(op));
        }
        else if (line.StartsWith("POP"))
        {
            Pop();
        }
        else if (line.StartsWith("JUMP_IF_FALSE"))
        {
            object cond = Pop();
            if (cond.GetType() == typeof(bool))
            {
                bool condb = (bool)cond;
                if (!condb)
                {
                    Jump(ParseAddress(op));
                }
            }
            else
            {
                //Debug.Log("ERROR: Expected boolean for JUMP_IF_FALSE.");
            }
        }
        else if (line.StartsWith("JUMP_INDIRECT"))
        {
            // TODO: Not sure about this
            Jump((int)heap[ParseAddress(op)]);
        }
        else if (line.StartsWith("JUMP"))
        {
            Jump(ParseAddress(op));
        }
        else if (line.StartsWith("EXTERN"))
        {
            string ext = op.Replace("\"", "");

            string[] splits = ext.Split(new string[] { "__" }, System.StringSplitOptions.None);
            string inputs = splits[2];
            string output = splits[3];
            int arity = inputs.Split('_').Length;

            int dst = -1;

            if (output != "SystemVoid")
            {
                dst = Pop();
            }

            externInvoker.SetProgramVariable("name", ext);
            for (int i = 0; i < arity; i++)
            {
                externInvoker.SetProgramVariable($"arg{i}", heap[Pop()]);
            }

            if (output == "SystemVoid")
            {
                externInvoker.SendCustomEvent($"InvokeExtern{arity}");
            }
            else
            {
                externInvoker.SendCustomEvent($"InvokeExtern{arity+1}");
                heap[dst] = externInvoker.GetProgramVariable($"arg{arity}");
            }
        }
        else if (line.StartsWith("COPY"))
        {
            int dst = Pop();
            int src = Pop();
            heap[dst] = heap[src]; 
        }

        if (sp > 0 && stack[sp-1] >= 0 && heap[stack[sp-1]] != null) output.text += ($"EXECUTED: {line}, pc: {pc-1}, sp: {sp}, top: {stack[sp-1]}, heap: {heap[stack[sp-1]]}") + "\n";
        if (sp > 0) output.text += ($"EXECUTED: {line}, pc: {pc-1}, sp: {sp}, top: {stack[sp-1]}") + "\n";
        else        output.text += ($"EXECUTED: {line}, pc: {pc-1}, sp: {sp}") + "\n";
    }
}
