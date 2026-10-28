using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// Herramienta de editor: menu "Isla Pirata > Rescate del jugador".
/// Crea (o actualiza) el objeto 'Rescate jugador' con PlayerRescue: si caes al mar vuelves a la barca,
/// si caes a la bodega del barco pirata vuelves a la cubierta, y al empezar activa Move y el CharacterController.
public static class RescueSetupTool
{
    [MenuItem("Isla Pirata/Rescate del jugador")]
    static void Create()
    {
        var origin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        var boat = Object.FindAnyObjectByType<BoatRowing>(FindObjectsInactive.Include);
        if (origin == null || boat == null) { Debug.LogError("[RESCATE] Falta el XR Origin o la barca (BoatRowing) en la escena."); return; }

        var rescue = Object.FindAnyObjectByType<PlayerRescue>(FindObjectsInactive.Include);
        if (rescue == null)
        {
            var go = new GameObject("Rescate jugador");
            Undo.RegisterCreatedObjectUndo(go, "Rescate");
            rescue = go.AddComponent<PlayerRescue>();
        }
        Undo.RecordObject(rescue, "Rescate");
        rescue.xrOrigin = origin.transform;
        rescue.head = origin.Camera != null ? origin.Camera.transform : null;
        rescue.boat = boat;
        var seat = boat.GetComponentsInChildren<TeleportationAnchor>(true).FirstOrDefault();
        rescue.boatSeat = seat != null ? seat.transform : boat.transform;
        // DEMO: el agua no tiene collider; su altura ~ un poco por encima del fondo del casco de la barca
        rescue.waterLevel = SetupToolUtils.RenderBounds(boat.gameObject).min.y + 0.3f;

        // Barco pirata y su cubierta: la escala de cuerda (Punto cubierta) o el ancla de la cubierta
        var ladder = Object.FindAnyObjectByType<RopeLadder>(FindObjectsInactive.Include);
        var part = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith("StylShip_Body"));   // solo en el editor
        rescue.ship = ladder != null && ladder.transform.parent != null ? ladder.transform.root : part != null ? part.root : null;
        Transform deck = ladder != null ? ladder.deckPoint : null;
        if (deck == null && rescue.ship != null)
        {
            var a = rescue.ship.GetComponentsInChildren<TeleportationAnchor>(true).FirstOrDefault();
            if (a != null) deck = a.transform;
        }
        rescue.deckPoint = deck;

        Selection.activeObject = rescue.gameObject;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rescue.gameObject.scene);
        Debug.Log($"[RESCATE] Listo: asiento de la barca '{rescue.boatSeat.name}', agua a y={rescue.waterLevel:F2}, " +
                  $"barco pirata '{(rescue.ship != null ? rescue.ship.name : "NO ENCONTRADO")}', cubierta '{(deck != null ? deck.name : "NO ENCONTRADA (Isla Pirata > Escala de cuerda)")}'. Guarda (Ctrl+S).");
    }
}
