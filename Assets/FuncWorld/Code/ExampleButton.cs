
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.UI;

public class ExampleButton : UdonSharpBehaviour
{
    public InputField input;
    public Compiler compiler;

    public void LoadExample()
    {
        input.text = gameObject.name.Replace("\\n", "\n");
        compiler.Compile();
    }
}
