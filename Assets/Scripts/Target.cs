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
    public float popupDuration = 1.2f;

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

    IEnumerator Popup(Vector3 position, int points, bool bullseye)
    {
        var cam = Camera.main;
        Vector3 toCam = cam ? (cam.transform.position - position).normalized : -transform.forward;

        var go = new GameObject("Popup +" + points);
        go.transform.position = position + toCam * 0.15f + Vector3.up * 0.1f;

        var text = go.AddComponent<TextMeshPro>();
        text.text = bullseye ? $"+{points}\n<size=50%>EN PLEIN CENTRE !</size>" : $"+{points}";
        text.fontSize = 3f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        Color color = bullseye ? new Color(1f, 0.85f, 0.1f) : points == 0 ? Color.gray : Color.white;
        text.color = color;

        for (float t = 0f; t < popupDuration; t += Time.deltaTime)
        {
            go.transform.position += Vector3.up * (0.4f * Time.deltaTime);
            if (cam) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            color.a = 1f - Mathf.Pow(t / popupDuration, 3f);
            text.color = color;
            yield return null;
        }

        Destroy(go);
    }
}
