using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

/// Herramienta de editor: menu "Isla Pirata > Activar mandos Quest (OpenXR)".
/// Activa los perfiles de mando de Meta Quest (Oculus Touch y Meta Quest Touch Plus) en OpenXR para Windows (Quest Link)
/// y Android (build en el casco). Sin ellos Unity no lee los mandos: no se ven ni se mueven.
/// Es lo mismo que anadirlos a mano en Project Settings > XR Plug-in Management > OpenXR > Enabled Interaction Profiles.
public static class OpenXRControllersFix
{
    [MenuItem("Isla Pirata/Activar mandos Quest (OpenXR)")]
    static void Fix()
    {
        int changed = 0;
        foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null) { Debug.LogWarning($"[OPENXR] No hay ajustes de OpenXR para {group} (¿OpenXR desactivado en XR Plug-in Management?)."); continue; }
            changed += Enable<OculusTouchControllerProfile>(settings, group);
            changed += Enable<MetaQuestTouchPlusControllerProfile>(settings, group);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[OPENXR] Perfiles de mando de Quest activados ({changed} cambios). Dale a Play otra vez con el casco conectado por Link. " +
                  "Incluye 'Assets/XR/Settings/OpenXR Package Settings.asset' en el commit.");
        EditorUtility.DisplayDialog("Mandos Quest", $"Perfiles Oculus Touch y Meta Quest Touch Plus activados ({changed} cambios).\n\nDale a Play otra vez.", "Vale");
    }

    static int Enable<T>(OpenXRSettings settings, BuildTargetGroup group) where T : OpenXRFeature
    {
        var f = settings.GetFeature<T>();
        if (f == null) { Debug.LogWarning($"[OPENXR] {typeof(T).Name} no existe para {group}."); return 0; }
        if (f.enabled) return 0;
        f.enabled = true;
        EditorUtility.SetDirty(f);
        EditorUtility.SetDirty(settings);
        Debug.Log($"[OPENXR] {group}: activado {typeof(T).Name}");
        return 1;
    }
}
