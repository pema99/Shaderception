Shader "Unlit/VM"
{
    SubShader
    {
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "VM.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 res : TEXCOORD1;
            };

            v2f vert (appdata IN)
            {
                v2f o;

                float4 result = runVM(IN.uv);
                IN.vertex.z = result.a;
                o.vertex = UnityObjectToClipPos(IN.vertex);
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