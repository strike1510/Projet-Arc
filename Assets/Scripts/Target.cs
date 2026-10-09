using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// À mettre sur la racine d'une cible (prefab Cible / CibleTrepied).
/// Détecte automatiquement les anneaux (enfants "disque0" à "disque4") et calcule
/// les points selon la distance au centre.
/// </summary>
public class Target : MonoBehaviour
{
    [Tooltip("Points de chaque anneau, de l'extérieur vers le centre (disque0 → disque4).")]
    public int[] ringPoints = { 2, 4, 6, 8, 10 };

    [Tooltip("Préfixe des enfants qui forment les anneaux.")]
    public string ringPrefix = "disque";

    [Header("Popup de points")]
    public bool showPopup = true;
    public float popupDuration = 1.6f;
    [Tooltip("Hauteur du texte au-dessus du bord de la cible (m).")]
    public float popupHeight = 0.35f;
    [Tooltip("Taille du texte par mètre de distance au joueur.")]
    public float popupSize = 0.7f;

    // Anneaux triés du plus grand (extérieur) au plus petit (centre)
    readonly List<Transform> rings = new();

    void Awake()
    {
        foreach (Transform child in transform)
            if (child.name.StartsWith(ringPrefix) && child.TryGetComponent(out MeshFilter _))
                rings.Add(child);

        rings.Sort((a, b) => b.localScale.x.CompareTo(a.localScale.x));

        if (rings.Count == 0)
        {
            Debug.LogError($"[Target] Aucun enfant '{ringPrefix}…' trouvé sous {name}.", this);
            return;
        }

        // Les disques ont des CapsuleCollider qui, aplatis, deviennent des SPHÈRES de 50 cm
        // devant la cible : on les remplace par des MeshCollider qui épousent vraiment les disques.
        foreach (var ring in rings)
        {
            if (ring.TryGetComponent(out CapsuleCollider capsule)) Destroy(capsule);
            if (!ring.TryGetComponent(out MeshCollider _))
                ring.gameObject.AddComponent<MeshCollider>().sharedMesh = ring.GetComponent<MeshFilter>().sharedMesh;
        }
    }

    /// <summary>
    /// Points pour un impact à cette position. ringIndex : 0 = extérieur, rings.Count-1 = centre, -1 = hors cible.
    /// </summary>
    public int GetPoints(Vector3 worldPoint, out int ringIndex)
    {
        ringIndex = -1;
        if (rings.Count == 0) return 0;

        // Distance au centre mesurée dans le plan du grand disque.
        // Un cylindre Unity a un rayon de 0.5 en local, sur les axes X et Z.
        Transform outer = rings[0];
        Vector3 local = outer.InverseTransformPoint(worldPoint);
        float distance = new Vector2(local.x, local.z).magnitude / 0.5f * outer.localScale.x;

        for (int i = rings.Count - 1; i >= 0; i--)
        {
            if (distance <= rings[i].localScale.x * 1.02f)   // 2 % de tolérance sur le bord
            {
                ringIndex = i;
                break;
            }
        }

        if (ringIndex < 0) return 0;
        if (ringIndex < ringPoints.Length) return ringPoints[ringIndex];
        return ringPoints.Length > 0 ? ringPoints[^1] : 0;
    }

    public bool IsBullseye(int ringIndex) => ringIndex == rings.Count - 1;

    /// <summary>Appelé par la flèche quand elle se plante dans la cible.</summary>
    public void OnArrowHit(Vector3 worldPoint)
    {
        int points = GetPoints(worldPoint, out int ring);
        bool bullseye = IsBullseye(ring);

        if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterHit(points, bullseye);
        else Debug.LogWarning("[Target] Pas de ScoreManager dans la scène : point non compté.");

        if (showPopup) StartCoroutine(Popup(worldPoint, points, bullseye));
    }

    IEnumerator Popup(Vector3 hitPoint, int points, bool bullseye)
    {
        var cam = Camera.main;
        Transform outer = rings[0];
        Vector3 center = outer.position;
        float radius = 0.5f * outer.lossyScale.x;

        // Au-dessus de la cible (pas dessus), légèrement vers le joueur
        Vector3 toCam = cam ? (cam.transform.position - center).normalized : -transform.forward;
        Vector3 start = center + Vector3.up * (radius + popupHeight) + toCam * 0.3f;

        // Taille proportionnelle à la distance : même lisibilité à 5 m ou à 30 m
        float distance = cam ? Vector3.Distance(cam.transform.position, start) : 10f;
        float baseSize = Mathf.Max(2f, distance * popupSize);

        var go = new GameObject("Popup +" + points);
        go.transform.position = start;

        var text = go.AddComponent<TextMeshPro>();
        text.text = bullseye ? $"+{points}\n<size=45%>EN PLEIN CENTRE !</size>" : points > 0 ? $"+{points}" : "0";
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineWidth = 0.3f;
        text.outlineColor = new Color32(0, 0, 0, 255);

        Color color = PointsColor(points, bullseye);

        for (float t = 0f; t < popupDuration; t += Time.deltaTime)
        {
            float k = t / popupDuration;

            // Petit effet "pop" au début, puis montée et fondu à la fin
            float pop = k < 0.15f ? Mathf.Lerp(0.4f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Min(1f, (k - 0.15f) / 0.15f));
            text.fontSize = baseSize * pop;
            go.transform.position = start + Vector3.up * (0.5f * k);
            if (cam) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);

            color.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            text.color = color;
            yield return null;
        }

        Destroy(go);
    }

    static Color PointsColor(int points, bool bullseye)
    {
        if (bullseye) return new Color(1f, 0.84f, 0f);          // or
        if (points >= 8) return new Color(1f, 0.35f, 0.25f);    // rouge vif
        if (points >= 4) return new Color(0.45f, 0.85f, 1f);    // bleu clair
        if (points > 0) return Color.white;
        return new Color(0.6f, 0.6f, 0.6f);                     // gris
    }
}
