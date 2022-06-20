
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ExternInvoker : UdonSharpBehaviour
{
    public object result;

    public void InvokeExtern(string name, object[] args)
    {
        args[0] = name;
    }

    public void InvokeExternVoid(string name, object[] args)
    {

    }
}
