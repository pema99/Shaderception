Shader "Shaderception/VM"
{
    Properties
    {
        _Camera ("Camera", 2D) = "white"{}
    }
    SubShader
    {
        Lighting Off

        Pass
        {
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #include "VM.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 5.0

            float4 frag(v2f_customrendertexture IN) : COLOR
            {
                return runVM(IN.localTexcoord.xy);
            }
            ENDCG
        }
    }
}