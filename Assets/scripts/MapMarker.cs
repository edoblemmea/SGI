using UnityEngine;

/// Un lugar que aparece en la carta (TreasureMap): muelle, mirador, palmeral, barco fantasma...
/// - 'Discovered': ya has estado (o se conoce desde el principio). Solo los descubiertos permiten viaje rapido.
/// - 'Hidden Until Objective': no sale en la carta hasta completar ese objetivo (p. ej. el barco, tras 'otear').
/// - 'Travel Point': donde apareces al viajar (si se deja vacio, el propio objeto).
/// Ponlo en un objeto vacio en el lugar (lo crea el menu "Isla Pirata > Carta").
public class MapMarker : MonoBehaviour
{
    [Tooltip("Nombre en la carta")]
    public string label = "Lugar";
    [Tooltip("Pista que se muestra en la carta junto al nombre (opcional)")]
    [TextArea] public string hint = "";
    public Color color = new Color(0.55f, 0.2f, 0.1f);

    [Header("Descubrir")]
    public bool discoveredAtStart = false;
    [Tooltip("Se descubre al acercarte a menos de esta distancia (m)")]
    public float discoverRadius = 8f;
    [Tooltip("No aparece en la carta hasta completar este objetivo del GameManager (vacio = siempre visible)")]
    public string hiddenUntilObjective = "";

    [Header("Viaje rapido")]
    public bool allowFastTravel = true;
    [Tooltip("Donde apareces al viajar aqui (sobre el suelo). Vacio = este objeto")]
    public Transform travelPoint;

    public bool Discovered { get; set; }
    public Transform Destination => travelPoint != null ? travelPoint : transform;

    void Awake() => Discovered = discoveredAtStart;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, discoverRadius);
        if (travelPoint != null) Gizmos.DrawLine(transform.position, travelPoint.position);
    }
}
