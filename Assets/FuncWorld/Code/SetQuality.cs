
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SetQuality : UdonSharpBehaviour
{
    public CustomRenderTexture buffer;
    public MeshRenderer screen;

    public override void Interact()
    {
        CustomRenderTexture rt = (CustomRenderTexture)screen.material.GetTexture("_MainTex");
        rt.updateMode = CustomRenderTextureUpdateMode.OnDemand;
        buffer.updateMode = CustomRenderTextureUpdateMode.Realtime;
        screen.material.SetTexture("_MainTex", buffer);
    }
}
