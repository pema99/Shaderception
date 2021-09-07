Shader "Shaderception/VM"
{
    Properties
    {
        _Camera ("Camera", 2D) = "white"{}
        _Video ("Video", 2D) = "white"{}
        [Toggle(_)] _IsAVProInput("Is AV Pro Input", Int) = 0
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