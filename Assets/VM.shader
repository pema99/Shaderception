Shader "Unlit/VM"
{
    SubShader
    {
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #pragma target 3.5

            #define glsl_mod(x,y) (((x)-(y)*floor((x)/(y)))) 

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 res : TEXCOORD1;
            };

            // Program binary
            float4 _Program[4000];

            // Stack machine
            static v2f varyings;
            static float4 stack[128];
            static int stackPtr = 0;
            static float4 vtab[256];

            // This is so monstrously cursed
            uint4 _Sentinel;
            float4 isSentinel(float4 val)
            {
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

            uint getDimension(float4 val)
            {
                return dot(1, isSentinel(val) ? 0 : 1);
            }

            float4 swizzle(float4 val, float4 mask)
            {
                float4 res = getSentinel();
                for (int i = 0; i < 4; i++)
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
                float4 res = curr;
                for (int i = 0; i < 4; i++)
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

            float4 getVar(int opi)
            {
                return vtab[opi % 256];
            }

            void setVar(int opi, uint rawMask, float4 val)
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

            int getFunArity(int opi)
            {
                [forcecase] switch(opi)
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
                    case 33: return 0;
                    default: return 0; 
                }
            }

            float4 callFun(int opi, float4x4 ops)
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
                    case 30: return float4(varyings.uv, getSentinel().xx);
                    case 31: return float4(2.0 * (varyings.uv - 0.5) * 10.0, getSentinel().xx);
                    case 32: return _Time;
                    case 33: return round(ops[0]);
                    default: return 0; 
                }
            }

            v2f vert (appdata IN)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(IN.vertex);
                o.uv = IN.uv;
                o.res = 0;

                varyings = o;
                stackPtr = 0;

                for (int i = 0; i < 1000; i += 2)
                {
                    int instr = round(_Program[i].x);
                    float4 opf = _Program[i + 1];
                    int opi = round(opf).x;
                    if (instr == 0)
                        break;

                    [forcecase] switch(instr)
                    {
                        case 1: // PUSHCONST <float>
                            stack[stackPtr] = opf;
                            stackPtr++;
                            break;
                        
                        case 2: // PUSHVAR <char>
                            stack[stackPtr] = getVar(opi);
                            stackPtr++;
                            break;

                        case 3: // BINOP <char>
                            stackPtr--;
                            float4 r = stack[stackPtr];
                            stackPtr--;
                            float4 l = stack[stackPtr];

                            // if dimensions mismatch, treat the smaller as full width
                            bool rSingle = getDimension(r) == 1;
                            bool lSingle = getDimension(l) == 1;
                            if      (rSingle && !lSingle) r = r.xxxx;
                            else if (!rSingle && lSingle) l = l.xxxx;
                            
                            [forcecase] switch(opi)
                            {
                                case 1:  stack[stackPtr] = l + r;  break;
                                case 2:  stack[stackPtr] = l - r;  break;
                                case 3:  stack[stackPtr] = l * r;  break;
                                case 4:  stack[stackPtr] = l / r;  break;
                                case 5:  stack[stackPtr] = l < r;  break;
                                case 6:  stack[stackPtr] = l > r;  break;
                                case 7:  stack[stackPtr] = l == r; break;
                                case 8:  stack[stackPtr] = l <= r; break;
                                case 9:  stack[stackPtr] = l >= r; break;
                                case 10: stack[stackPtr] = l != r; break;
                                case 11: stack[stackPtr] = l && r; break;
                                case 12: stack[stackPtr] = l || r; break;
                                default: break;
                            }
                            stackPtr++;
                            break;

                        case 4: // UNOP <char>
                            stackPtr--;
                            float4 rr = stack[stackPtr];
                            stack[stackPtr] = opi == '-' ? -rr : rr;
                            stackPtr++;
                            break;

                        case 5: // CALL <int>
                            float4x4 v = 0;
                            int arity = getFunArity(opi);
                            for (int k = 0; k < arity; k++)
                            {
                                stackPtr--;
                                v[k] = maskSentinel(stack[stackPtr], 0);
                            }
                            float4x4 rev = 0;
                            for (int j = 0; j < arity; j++)
                            {
                                rev[j] = v[arity-1-j];
                            }
                            // TODO: Restore sentinel values after function call
                            stack[stackPtr] = callFun(opi, rev);
                            stackPtr++;
                            break;

                        case 6: // SETVAR <char>
                            stackPtr--;
                            float4 val = stack[stackPtr];
                            setVar(opi, opf.y, val);
                            break;

                        case 7: // JUMP <location>
                            i = opi;
                            break;

                        case 8: // CONDJUMP <location>
                            stackPtr--;
                            float4 cond = stack[stackPtr];
                            if (cond.x == 0)
                                i = opi;
                            break;

                        case 9: // LABEL <nop>
                            break;
                    }
                }

                stackPtr--;
                stackPtr = max(stackPtr, 0);
                
                float4 result = maskSentinel(stack[stackPtr], 0);
                IN.vertex.z = result.a;
                
                o.vertex = UnityObjectToClipPos(IN.vertex);
                o.uv = IN.uv;
                o.res = result.rgb;
                
                return o;
            }

            float4 frag (v2f IN) : SV_Target
            {
                return float4(IN.res, 1);
            }
            ENDCG
        }
    }
}