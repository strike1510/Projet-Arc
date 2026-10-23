using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Tout ce qu'une flèche (ou le TestShooter) peut toucher en dehors des cibles : ninjas, animaux…
/// ArrowHit et TestShooter appellent OnArrowHit sur le premier composant qui l'implémente.
/// </summary>
public interface IArrowHittable
{
    /// <summary>Touché par une flèche au point donné, la flèche allant dans la direction donnée.</summary>
    void OnArrowHit(Vector3 point, Vector3 direction);

    /// <summary>Faux une fois mort / caché : la flèche passe à travers.</summary>
    bool CanBeHit { get; }
}

/// <summary>Petit texte qui monte et disparaît (ex. "+50 NINJA"). Utilisable de n'importe où.</summary>
public static class HitPopup
{
    class Host : MonoBehaviour { }
    static Host host;

    public static void Show(Vector3 position, string text, Color color, float size = 0.7f, float duration = 1.6f)
    {
        if (host == null)
        {
            var go = new GameObject("(popups)");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
        }
        host.StartCoroutine(Run(position, text, color, size, duration));
    }

    static IEnumerator Run(Vector3 start, string text, Color color, float size, float duration)
    {
        var cam = TargetSpawner.FindGameViewCamera();
        float distance = cam ? Vector3.Distance(cam.transform.position, start) : 10f;
        float baseSize = Mathf.Max(2f, distance * size);

        var go = new GameObject("Popup");
        go.transform.position = start;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);

        for (float t = 0f; t < duration && go != null; t += Time.deltaTime)
        {
            float k = t / duration;
            float pop = k < 0.15f ? Mathf.Lerp(0.4f, 1.2f, k / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Min(1f, (k - 0.15f) / 0.15f));
            tmp.fontSize = baseSize * pop;
            go.transform.position = start + Vector3.up * (0.6f * k);
            if (cam) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            color.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            tmp.color = color;
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }
}
