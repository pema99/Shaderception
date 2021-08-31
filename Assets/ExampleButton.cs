
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class ExampleButton : UdonSharpBehaviour
{
    public string ExampleProgram;
    public InputField input;

    public override void Interact()
    {
        input.text = ExampleProgram.Replace("\\n", "\n");
    }
}
