using UnityEngine;

/// <summary>
/// Fiche d'un arc : son prefab, ses stats et son effet spécial.
/// Création : clic droit dans le dossier Project > Create > Arcs > Preset d'arc.
/// Les stats sont copiées dans le BowController de l'arc quand le sélecteur le fait apparaître.
/// </summary>
[CreateAssetMenu(fileName = "NouvelArc", menuName = "Arcs/Preset d'arc")]
public class BowData : ScriptableObject
{
    [Header("Présentation")]
    public string displayName = "Arc";

    [TextArea(2, 3)]
    public string description = "";

    [Tooltip("Couleur du titre et des barres de stats sur le panneau.")]
    public Color color = WoodUI.Gold;

    [Tooltip("Prefab de l'arc (Bow_Base, Bow_Bio, ...).")]
    public GameObject prefab;

    [Header("Stats (copiées dans BowController)")]
    [Tooltip("Vitesse de la flèche à tension minimale (m/s).")]
    public float minSpeed = 5f;

    [Tooltip("Vitesse de la flèche à pleine tension (m/s).")]
    public float maxSpeed = 30f;

    [Tooltip("Distance de tension maximale de la corde (m).")]
    public float maxDraw = 0.5f;

    [Header("Effet spécial (vide = aucun)")]
    public string effectName = "";

    [TextArea(2, 3)]
    public string effectDescription = "";

    public bool HasEffect => !string.IsNullOrWhiteSpace(effectName);

    public void ApplyTo(BowController bow)
    {
        if (bow == null) return;
        bow.minSpeed = minSpeed;
        bow.maxSpeed = maxSpeed;
        bow.maxDraw = maxDraw;
    }
}