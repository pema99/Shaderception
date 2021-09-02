
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SetQuality : UdonSharpBehaviour
{
    public Mesh mesh;
    public MeshFilter renderer;

    public override void Interact()
    {
        renderer.mesh = mesh;
    }
}
