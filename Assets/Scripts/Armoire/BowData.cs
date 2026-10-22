using UnityEngine;

/// <summary>
/// Fiche d'un arc : son prefab, ses stats et son effet spécial.
/// Création : clic droit dans le dossier Project > Create > Tir à l'arc > Données d'arc.
/// Les stats sont copiées dans le BowController de l'arc quand l'armoire le fait apparaître.
/// </summary>
[CreateAssetMenu(fileName = "NouvelArc", menuName = "Tir à l'arc/Données d'arc")]
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

    [Header("Effets en jeu")]
    [Tooltip("Chaque tir lance aussi 2 flèches bonus, une de chaque côté.")]
    public bool tripleShot;

    [Tooltip("Écart entre la flèche principale et les flèches bonus (degrés).")]
    [Range(0f, 20f)] public float tripleSpread = 4f;

    [Tooltip("Part des points donnée par une flèche bonus qui touche une cible (0.5 = moitié).")]
    [Range(0f, 1f)] public float bonusPointsFactor = 0.5f;

    [Tooltip("Les flèches laissent une traînée discrète de feuilles et de spores.")]
    public bool leafTrail;

    [Tooltip("La flèche se change en éclair pendant le vol et laisse une brûlure à l'impact.")]
    public bool lightning;

    [Tooltip("Durée de la brûlure laissée par la foudre (s).")]
    public float burnDuration = 2f;

    public bool HasEffect => !string.IsNullOrWhiteSpace(effectName);
    public bool HasGameplayEffect => tripleShot || leafTrail || lightning;

    public void ApplyTo(BowController bow)
    {
        if (bow == null) return;
        bow.minSpeed = minSpeed;
        bow.maxSpeed = maxSpeed;
        bow.maxDraw = maxDraw;
    }
}
