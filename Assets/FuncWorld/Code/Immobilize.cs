
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Immobilize : UdonSharpBehaviour
{
    public override void Interact()
    {
        // If we've changed the values from the default, restore them to the default.
        if (Networking.LocalPlayer.GetStrafeSpeed() == 0)
        {
            Networking.LocalPlayer.SetStrafeSpeed();
            Networking.LocalPlayer.SetRunSpeed();
            Networking.LocalPlayer.SetWalkSpeed();
            Networking.LocalPlayer.SetJumpImpulse();
        }
        // Otherwise, lock the player in place.
        else
        {
            Networking.LocalPlayer.SetStrafeSpeed(0);
            Networking.LocalPlayer.SetRunSpeed(0);
            Networking.LocalPlayer.SetWalkSpeed(0);
            Networking.LocalPlayer.SetJumpImpulse(0);
        }
    }
}
