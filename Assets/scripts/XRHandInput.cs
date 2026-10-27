using UnityEngine;
using UnityEngine.XR;

/// Utilidades para leer los mandos DIRECTAMENTE del casco (XR InputDevices), igual que BoatHandRowing:
/// funciona aunque los objetos Left/Right Controller esten desactivados por el XR Input Modality Manager.
/// No es un componente: se usa desde otros scripts (XRHandInput.Grip(XRNode.LeftHand)...).
public static class XRHandInput
{
    /// Posicion de la mano en el mundo. trackingSpace = "Camera Offset" (padre de Main Camera).
    public static bool TryGetWorldPosition(XRNode node, Transform trackingSpace, Transform fallback, out Vector3 world)
    {
        world = default;
        var dev = InputDevices.GetDeviceAtXRNode(node);
        if (dev.isValid && trackingSpace != null &&
            dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 p))
        {
            world = trackingSpace.TransformPoint(p);
            return true;
        }
        if (fallback != null && fallback.gameObject.activeInHierarchy)
        {
            world = fallback.position;
            return true;
        }
        return false;
    }

    public static float Grip(XRNode node)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        return dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float g) ? g : 0f;
    }

    /// Boton A (mando derecho) / X (mando izquierdo)
    public static bool PrimaryButton(XRNode node)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        return dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool b) && b;
    }

    public static void Haptic(XRNode node, float amplitude, float duration)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        if (dev.isValid) dev.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
    }

    public static void HapticBoth(float amplitude, float duration)
    {
        Haptic(XRNode.LeftHand, amplitude, duration);
        Haptic(XRNode.RightHand, amplitude, duration);
    }
}
