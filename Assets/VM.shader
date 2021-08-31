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
                o.uv = 2.0 * (v.uv - 0.5) * 10.0;
                return o;
            }

            float _Program[1000];

            float getVar(v2f IN, int opi)
            {
                switch (opi)
                {
                    case 'x': return IN.uv.x;
                    case 'y': return IN.uv.y;
                    case 't': return _Time.y;
                    default:  return 0;
                }
            }

            int getFunArity(int opi)
            {
                switch (opi)
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
                switch (opi)
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
                float stack[1000]; int stackPtr = 0;
                for (int i = 0; i < 1000; i += 2)
                {
                    int instr = round(_Program[i]);
                    float opf = _Program[i + 1];
                    int opi = round(opf);
                    if (instr == 0)
                        break;

                    switch (instr)
                    {
                        case 1: // PUSHCONST <float>
                            stack[stackPtr] = opf;
                            stackPtr++;
                            break;
                        
                        case 2: // PUSHVAR <char>
                            stack[stackPtr] = getVar(IN, opi);
                            stackPtr++;
                            break;

                        case 3: // BINOP <char>
                            stackPtr--;
                            float r = stack[stackPtr];
                            stackPtr--;
                            float l = stack[stackPtr];
                            switch (opi)
                            {
                                case '+': stack[stackPtr] = l + r; break;
                                case '-': stack[stackPtr] = l - r; break;
                                case '*': stack[stackPtr] = l * r; break;
                                case '/': stack[stackPtr] = l / r; break;
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
                            int i = 0;
                            for (; i < arity; i++)
                            {
                                stackPtr--;
                                v[i] = stack[stackPtr];
                            }
                            float4 rev = 0;
                            for (int j = 0; j < i; j++)
                            {
                                rev[j] = v[i-1-j];
                            }
                            stack[stackPtr] = callFun(opi, rev);
                            stackPtr++;
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
