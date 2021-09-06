// Shader roperties
float4 _InputButton;
float4 _InputAxis;
sampler2D _Camera;

// Program binary
cbuffer ProgramBuffer {
    float4 _Program[1023*4] : packoffset(c0);  
    float4 _Program0[1023] : packoffset(c0);
    float4 _Program1[1023] : packoffset(c1023);
    float4 _Program2[1023] : packoffset(c2046);
    float4 _Program3[1023] : packoffset(c3069);
};

// The one and only
#define glsl_mod(x,y) (((x)-(y)*floor((x)/(y)))) 

// Stack machine
static float4 stack[128];
static uint stackPtr = 0;
static float4 vtab[256];

// Other globals
static float2 globaluv;
static uint jumpCount = 0;
static const float MAX_JUMPS = 2000;

// Sentinel handling
uint4 _Sentinel;
float4 isSentinel(float4 val)
{
    // XOR with an unbound uniform to trick the compiler into
    // keeping the isnan check.
    return isnan(asfloat(_Sentinel ^ asuint(val)));
}

float4 getSentinel()
{
    return asfloat(-1);
}

float4 maskSentinel(float4 val, float to)
{
    return isSentinel(val) ? to : val;
}

float4 restoreSentinel(float4 val, float4 comp)
{
    return isSentinel(comp) ? comp : val;
}

uint getDimension(float4 val)
{
    return dot(1, isSentinel(val) ? 0 : 1);
}

float4 dynamicCast(float4 val, uint from, uint to)
{
    if (from == to)
    {
        return val;
    }
    else if (from == 1)
    {
        [forcecase] switch (to)
        {
            case 0:  return getSentinel();
            case 1:  return float4(val.x, getSentinel().xxx);
            case 2:  return float4(val.xx, getSentinel().xx);
            case 3:  return float4(val.xxx, getSentinel().x);
            case 4:  return val.xxxx;
            default: return val;
        }
    }
    else
    {
        [forcecase] switch (to)
        {
            case 0:  return getSentinel();
            case 1:  return float4(val.x, getSentinel().xxx);
            case 2:  return float4(val.xy, getSentinel().xx);
            case 3:  return float4(val.xyz, getSentinel().x);
            default: return val;
        }
    }
}

// Swizzling
float4 swizzle(float4 val, float4 mask)
{
    float4 res = getSentinel();
    for (uint i = 0; i < 4; i++)
    {
        if (mask[i] > 0)
        {
            res[i] = val[mask[i]-1];
        }
    }
    return res;
}

float4 swizzleAssign(float4 curr, float4 val, uint rawMask)
{
    // Find redirection mask
    float4 mask = float4((rawMask % 10000) / 1000, (rawMask % 1000) / 100, (rawMask % 100) / 10, rawMask % 10);
    while (mask.x == 0)
    {
        mask.xyz = mask.yzw;
        mask.w = 0; 
    }

    // Assign each field (manually to satisfy shador compiler chan)
    val = maskSentinel(val, 0);
    float4 res = curr;
    for (uint i = 0; i < 4; i++)
    {
        if (mask[i] > 0)
        {
            [forcecase] switch (mask[i]-1)
            {
                case 0: res.x = val[i]; break;
                case 1: res.y = val[i]; break;
                case 2: res.z = val[i]; break;
                case 3: res.w = val[i]; break;
            }
        }
    }

    return res;
}

// Vtable operations
float4 getVar(uint opi)
{
    return vtab[opi % 256];
}

void setVar(uint opi, uint rawMask, float4 val)
{
    if (rawMask > 0)
    {
        vtab[opi % 256] = swizzleAssign(vtab[opi % 256], val, rawMask);
    }
    else
    {
        vtab[opi % 256] = val;
    }
}

// Stack operations
void pushStack(float4 val)
{
    stack[stackPtr] = val;
    stackPtr++;
}

float4 popStack()
{
    stackPtr--;
    return stack[stackPtr % 128];
}

// Builtin functions
float4 getFunSentinelMask(uint opi, uint arity, float4x4 ops)
{
    // Note to future Pema:
    //   This uber cursed function returns the mask used to set
    //   sentinality of the value returned from a function based
    //   on the inputs. For swizzles and vector constructors, we
    //   don't want to set sentinality based on inputs, so this
    //   skips those. Remember, the ops matrix is an reverse order.
    if ((opi >= 1 && opi <= 25) || opi == 37  || opi == 39 || opi == 40)
    {
        //return ops[arity-1];
        uint maxDim = 0;
        for (uint i = 0; i < arity; i++)
        {
            maxDim = max(maxDim, getDimension(ops[arity-1-i]));
        }
        [forcecase] switch (maxDim)
        {
            case 1:  return float4(0, getSentinel().xxx);
            case 2:  return float4(0, 0, getSentinel().xx);
            case 3:  return float4(0, 0, 0, getSentinel().x);
            default: return 0;
        }
    }
    else
    {
        return 0;
    }
}

// x = arity, y = should pad smaller operand?
uint2 getFunInfo(uint opi)
{
    [forcecase] switch(opi)
    {
        case 1:  return uint2(1, 0);
        case 2:  return uint2(1, 0);
        case 3:  return uint2(1, 0);
        case 4:  return uint2(1, 0);
        case 5:  return uint2(1, 0);
        case 6:  return uint2(1, 0);
        case 7:  return uint2(1, 0);
        case 8:  return uint2(1, 0);
        case 9:  return uint2(2, 0);
        case 10: return uint2(1, 0);
        case 11: return uint2(1, 0);
        case 12: return uint2(1, 0);
        case 13: return uint2(1, 0);
        case 14: return uint2(1, 0);
        case 15: return uint2(1, 0);
        case 16: return uint2(1, 0);
        case 17: return uint2(1, 0);
        case 18: return uint2(1, 0);
        case 19: return uint2(2, 1);
        case 20: return uint2(2, 1);
        case 21: return uint2(2, 1);
        case 22: return uint2(3, 1);
        case 23: return uint2(3, 1);
        case 24: return uint2(2, 1);
        case 25: return uint2(3, 1);
        case 26: return uint2(2, 0);
        case 27: return uint2(3, 0);
        case 28: return uint2(4, 0);
        case 29: return uint2(2, 0);
        case 30: return uint2(0, 0);
        case 31: return uint2(0, 0);
        case 32: return uint2(0, 0);
        case 33: return uint2(0, 0);
        case 34: return uint2(2, 1);
        case 35: return uint2(2, 1);
        case 36: return uint2(2, 1);
        case 37: return uint2(1, 0);
        case 38: return uint2(1, 0);
        case 39: return uint2(2, 1);
        case 40: return uint2(3, 1);
        case 41: return uint2(1, 0);
        case 42: return uint2(0, 0);
        case 43: return uint2(0, 0);
        case 44: return uint2(0, 0);
        case 45: return uint2(1, 0);
        default: return uint2(0, 0); 
    }
}

float4 callFun(uint opi, float4x4 ops)
{
    [forcecase] switch(opi)
    {
        case 1: return log(ops[0]);
        case 2: return log2(ops[0]);
        case 3: return sin(ops[0]);
        case 4: return cos(ops[0]);
        case 5: return tan(ops[0]);
        case 6: return asin(ops[0]);
        case 7: return acos(ops[0]);
        case 8: return atan(ops[0]);
        case 9: return pow(ops[0], ops[1]);
        case 10: return exp(ops[0]);
        case 11: return exp2(ops[0]);
        case 12: return sqrt(ops[0]);
        case 13: return rsqrt(ops[0]);
        case 14: return abs(ops[0]);
        case 15: return sign(ops[0]);
        case 16: return floor(ops[0]);
        case 17: return ceil(ops[0]);
        case 18: return frac(ops[0]);
        case 19: return glsl_mod(ops[0], ops[1]);
        case 20: return min(ops[0], ops[1]);
        case 21: return max(ops[0], ops[1]);
        case 22: return clamp(ops[0], ops[1], ops[2]);
        case 23: return lerp(ops[0], ops[1], ops[2]);
        case 24: return step(ops[0], ops[1]);
        case 25: return smoothstep(ops[0], ops[1], ops[2]);
        case 26: return float4(ops[0].x, ops[1].x, getSentinel().xx);
        case 27: return float4(ops[0].x, ops[1].x, ops[2].x, getSentinel().x);
        case 28: return float4(ops[0].x, ops[1].x, ops[2].x, ops[3].x);
        case 29: return swizzle(ops[0], ops[1]);
        case 30: return float4(globaluv, getSentinel().xx);
        case 31: return float4(2.0 * (globaluv - 0.5) * 10.0, getSentinel().xx);
        case 32: return _Time;
        case 33: return round(ops[0]);
        case 34: return float4(dot(ops[0], ops[1]), getSentinel().xxx);
        case 35: return float4(cross(ops[0], ops[1]), getSentinel().x);
        case 36: return float4(distance(ops[0], ops[1]), getSentinel().xxx);
        case 37: return normalize(ops[0]);
        case 38: return float4(length(ops[0]), getSentinel().xxx);
        case 39: return reflect(ops[0], ops[1]);
        case 40: return refract(ops[0], ops[1], ops[2].x);
        case 41: return tex2Dlod(_SelfTexture2D, float4(ops[0].xy, 0, 0));
        case 42: return float4(_CustomRenderTextureWidth, _CustomRenderTextureHeight, getSentinel().xx);
        case 43: return _InputButton;
        case 44: return _InputAxis;
        case 45: return float4(tex2Dlod(_Camera, float4(ops[0].xy, 0, 0)).xyz, getSentinel().x);
        default: return 0; 
    }
}

float4 runVM(float2 uv)
{
    globaluv = uv;
    stackPtr = 0;

    // Hack to prevent unity from deleting aliased cbuffer
    if (uv.x < 0) globaluv = _Program0[0] + _Program1[0] + _Program2[0] + _Program3[0]; 

    for (uint i = 0; i < 4092; i += 2)
    {
        if (jumpCount > MAX_JUMPS)
            break;

        uint instr = round(_Program[i].x);
        float4 opf = _Program[i + 1];
        uint opi = round(opf).x;
        if (instr == 0)
            break;

        [forcecase] switch(instr)
        {
            case 1: // PUSHCONST <float>
                pushStack(opf);
                break;
            
            case 2: // PUSHVAR <char>
                pushStack(getVar(opi));
                break;

            case 3: // BINOP <char>
                float4 r = popStack();
                float4 l = popStack();

                // if dimensions mismatch, treat the smaller as full width
                uint rDim = getDimension(r);
                uint lDim = getDimension(l);
                bool rSingle = rDim == 1;
                bool lSingle = lDim == 1;
                if      (rSingle && !lSingle) r = dynamicCast(r, rDim, max(rDim, lDim));
                else if (!rSingle && lSingle) l = dynamicCast(l, lDim, max(rDim, lDim));

                [forcecase] switch(opi)
                {
                    case 1:  pushStack(l + r);  break;
                    case 2:  pushStack(l - r);  break;
                    case 3:  pushStack(l * r);  break;
                    case 4:  pushStack(l / r);  break;
                    case 5:  pushStack(l < r);  break;
                    case 6:  pushStack(l > r);  break;
                    case 7:  pushStack(l == r); break;
                    case 8:  pushStack(l <= r); break;
                    case 9:  pushStack(l >= r); break;
                    case 10: pushStack(l != r); break;
                    case 11: pushStack(l && r); break;
                    case 12: pushStack(l || r); break;
                    default: break;
                }
                break;

            case 4: // UNOP <char>
                float4 rr = popStack();
                pushStack(opi == '-' ? -rr : rr);
                break;

            case 5: // CALL <uint>
                uint2 info = getFunInfo(opi);
                uint arity = info.x;
                bool pad = info.y == 1;

                float4x4 vals = 0;
                uint4 dims = 0;
                uint maxDim = 0;
                for (uint k = 0; k < arity; k++)
                {
                    float4 val = popStack();
                    dims[k] = getDimension(val);
                    maxDim = max(maxDim, dims[k]);
                    vals[k] = val;
                }

                float4x4 masked = float4x4(
                    maskSentinel(vals[0], 0),
                    maskSentinel(vals[1], 0),
                    maskSentinel(vals[2], 0),
                    maskSentinel(vals[3], 0)
                );
                float4x4 rev = 0;
                for (uint j = 0; j < arity; j++)
                {
                    float4 vall = masked[arity-1-j];
                    uint dim = dims[arity-1-j];
                    if (pad)
                    {
                        vall = maskSentinel(dynamicCast(vall, dim, maxDim), 0);
                    }
                    rev[j] = vall;
                }

                float4 retVal = callFun(opi, rev);
                retVal = restoreSentinel(retVal, getFunSentinelMask(opi, arity, vals));
                pushStack(retVal);
                break;

            case 6: // SETVAR <char, mask>
                float4 val = popStack();
                setVar(opi, opf.y, val);
                break;

            case 7: // JUMP <location>
                i = opi - 2;
                jumpCount++;
                break;

            case 8: // CONDJUMP <location>
                float4 cond = popStack();
                if (cond.x == 0)
                {
                    i = opi - 2;
                    jumpCount++;
                }
                break;

            default:
                break;
        }
    }

    float4 result = popStack();
    return maskSentinel(dynamicCast(result, getDimension(result), 4), 0);
}