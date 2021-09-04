# Shaderception
A compiler written in UdonSharp that compiles shaders written in a small shading language to a stack based instruction set, which runs in a VM written in a shader. Shaderception. For use in VRChat: https://vrchat.com/home/launch?worldId=wrld_4d1a8927-452c-486d-af11-949a9aac58c3

My friend Fuopy wrote an awesome playable pong game using this that you can try out, and plans to collect some cool programs in a separate repo. Check it out here: https://github.com/fuopy/shaderception-apps

# Example program
Here is a small raymarcher that is capable at running at interactive speeds in the VM.

```glsl
fun smin(d1, d2, k)
{
    let h = clamp(0.5 + 0.5 * (d2 - d1) / k, 0.0, 1.0);
    lerp(d2, d1, h) - k * h * (1.0 - h)
}

fun map(p)
{
    let d1 = length(p) - 0.3;
    let d2 = length(p + float3(0.2, sin(time().y)*0.3, 0)) - 0.3;
    smin(d1, d2, 0.05)
}

fun march(ro, rd)
{
    let t = 0;
    let i = 0;
    while (i < 15)
    {
        let dist = map(ro + t * rd);
        let t = t + dist;
        let i = i + 1;
    }
    t
}

let p = 2.0 * (uv() - 0.5);
let ro = float3(0.0, 0.0, -1.0);
let rd = normalize(float3(p.x, p.y, 1));

let d = march(ro, rd);
if (d < 1)
{
    let hit = ro + d * rd;
    let a = map(hit+float3(0.01, 0, 0)) - map(hit-float3(0.01, 0, 0));
    let b = map(hit+float3(0, 0.01, 0)) - map(hit-float3(0, 0.01, 0));
    let c = map(hit+float3(0, 0, 0.01)) - map(hit-float3(0, 0, 0.01));
    normalize(float3(a, b, c)) * 0.5 + 0.5
}
else
{
    0
}
```



# VM Instruction Set Architecture 

```
Every instruction consists of 2 float4s, opcode and operand (wasteful I know, bite me).
The opcode goes in the X channel of the first float4.

Instructions (opcode - mnemonic <operand> - description):
1 - PUSHCONST <float4> - Push a constant to the stack
2 - PUSHVAR <id> - Push a variable stored at location <id> to the stack
3 - BINOP <op> - Take the 2 topmost elements of the stack, perform binary operation.
Put the result back on the stack.
4 - UNOP <op> - Take the topmost stack element and perform unary operation on it,
put result on stack.
5 - CALL <id> - Call builtin function with the given ID. Parameters should be on the
stack when the instruction is invoked. Result is put on the stack.
6 - SETVAR <id> - Pop a value off the stack and set the variable at location <id> to
the value. This operator also stores a mask of which elements of the vector to set in
the y element of the opcode. An example of such a mask could be the float "1223.0"
which means var.xyyz = <value>.
7 - JUMP <loc> - Jump to memory location.
8 - CONDJUMP <loc> - Pop value off the stack. If it is false (equals 0), jump to location.

Every function is inlined immediately. There is no callstack.

The only supported types are float, float2, float3 and float4. The type of a value is encoded
directly in the value. Every value is a full width float4, but elements that are not used will
have the value NaN. Thus, a 2D float vector may look like float4(1, 3, NaN, NaN).

Binary operator IDs from 1 to 12: +, -, *, /, <, >, ==, <=, >=, !=, &&, ||
Unary operator only has negation which is ID 45.

System call / builtin function IDs:
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
```
