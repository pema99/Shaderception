Shader "Unlit/VM"
{
    SubShader
    {
        Pass
        {
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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Program binary
            float _Program[1000];

            // Stack machine
            static v2f varyings;
            static float stack[1000];
            static int stackPtr = 0;
            static float vtab[256];

            float getVar(int opi)
            {
                [forcecase] switch (opi)
                {
                    case 'x': return 2.0 * (varyings.uv.x - 0.5) * 10.0;
                    case 'y': return 2.0 * (varyings.uv.y - 0.5) * 10.0;
                    case 'u': return varyings.uv.x;
                    case 'v': return varyings.uv.y;
                    case 't': return _Time.y;
                    default:  return vtab[opi % 256];
                }
            }

            void setVar(int opi, float val)
            {
                vtab[opi % 256] = val;
            }

            int getFunArity(int opi)
            {
                [forcecase] switch (opi)
                {
                    case 1: return 1;
                    case 2: return 1;
                    case 3: return 1;
                    case 4: return 1;
                    case 5: return 1;
                    case 6: return 1;
                    case 7: return 1;
                    case 8: return 1;
                    case 9: return 2;
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
                    default: return 0; 
                }
            }

            float callFun(int opi, float4 ops)
            {
                [forcecase] switch (opi)
                {
                    case 1: return log(ops.x);
                    case 2: return log2(ops.x);
                    case 3: return sin(ops.x);
                    case 4: return cos(ops.x);
                    case 5: return tan(ops.x);
                    case 6: return asin(ops.x);
                    case 7: return acos(ops.x);
                    case 8: return atan(ops.x);
                    case 9: return pow(ops.x, ops.y);
                    case 10: return exp(ops.x);
                    case 11: return exp2(ops.x);
                    case 12: return sqrt(ops.x);
                    case 13: return rsqrt(ops.x);
                    case 14: return abs(ops.x);
                    case 15: return sign(ops.x);
                    case 16: return floor(ops.x);
                    case 17: return ceil(ops.x);
                    case 18: return frac(ops.x);
                    case 19: return glsl_mod(ops.x, ops.y);
                    case 20: return min(ops.x, ops.y);
                    case 21: return max(ops.x, ops.y);
                    case 22: return clamp(ops.x, ops.y, ops.z);
                    case 23: return lerp(ops.x, ops.y, ops.z);
                    case 24: return step(ops.x, ops.y);
                    case 25: return smoothstep(ops.x, ops.y, ops.z);
                    default: return 0; 
                }
            }

            float4 frag (v2f IN) : SV_Target
            {
                varyings = IN;
                stackPtr = 0;

                for (int i = 0; i < 1000; i += 2)
                {
                    int instr = round(_Program[i]);
                    float opf = _Program[i + 1];
                    int opi = round(opf);
                    if (instr == 0)
                        break;

                    [forcecase] switch (instr)
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
                            float r = stack[stackPtr];
                            stackPtr--;
                            float l = stack[stackPtr];
                            [forcecase] switch (opi)
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
                            float rr = stack[stackPtr];
                            stack[stackPtr] = opi == '-' ? -rr : rr;
                            stackPtr++;
                            break;

                        case 5: // CALL <int>
                            float4 v = 0;
                            int arity = getFunArity(opi);
                            int k = 0;
                            for (; k < arity; k++)
                            {
                                stackPtr--;
                                v[k] = stack[stackPtr];
                            }
                            float4 rev = 0;
                            for (int j = 0; j < k; j++)
                            {
                                rev[j] = v[k-1-j];
                            }
                            stack[stackPtr] = callFun(opi, rev);
                            stackPtr++;
                            break;

                        case 6: // SETVAR <char>
                            stackPtr--;
                            float val = stack[stackPtr];
                            setVar(opi, val);
                            break;

                        case 7: // JUMP <location>
                            i = opi;
                            break;

                        case 8: // CONDJUMP <location>
                            stackPtr--;
                            float cond = stack[stackPtr];
                            if (cond == 0)
                                i = opi;
                            break;

                        case 9: // LABEL <nop>
                            break;
                    }
                }
                stackPtr--;
                stackPtr = max(stackPtr, 0);
                return float4(stack[stackPtr], 0, 0, 1);
            }
            ENDCG
        }
    }
}
