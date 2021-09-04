
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class CameraToggle : UdonSharpBehaviour
{
    public Camera cam;

    public override void Interact()
    {
        cam.enabled = !cam.enabled;
    }
}
