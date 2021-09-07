
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class SyncCode : UdonSharpBehaviour
{
    public InputField input;
    public Toggle toggle;
    public Compiler compiler;

    [UdonSynced] public string code = "";

    public override void OnDeserialization()
    {
        if (toggle.isOn)
        {
            input.text = code;
            compiler.Compile();
        }
    }

    public void PerformSync()
    {
        if (toggle.isOn)
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            code = input.text;
            compiler.Compile();
            RequestSerialization();
        }
    }
}
