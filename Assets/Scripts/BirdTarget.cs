using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Oiseau bonus : à mettre sur la racine d'un oiseau (ex. lb_crow, lb_robin... du pack "living birds").
/// Touché par une flèche → +100 points (ou la valeur choisie), petit texte au-dessus, et l'oiseau s'envole.
///
/// Pas besoin de collider : la flèche teste elle-même si sa trajectoire passe près de l'oiseau
/// (rayon "hitRadius" autour du centre du modèle). Ça ne gêne donc pas les scripts du pack.
/// </summary>
public class BirdTarget : MonoBehaviour
{
    // Tous les oiseaux actifs de la scène (ArrowHit les parcourt)
    static readonly List<BirdTarget> all = new List<BirdTarget>();
    public static IReadOnlyList<BirdTarget> All => all;

    [Header("Score")]
    [Tooltip("Points gagnés quand on touche l'oiseau.")]
    public int points = 100;

    [Tooltip("Rayon de la zone touchable autour du centre de l'oiseau (m). Plus grand = plus facile.")]
    public float hitRadius = 0.3f;

    public enum Reaction { SEnvole, Eclate }

    [Header("Quand on le touche")]
    [Tooltip("SEnvole : l'oiseau s'enfuit. Eclate : l'objet disparaît d'un coup (pomme...).")]
    public Reaction reaction = Reaction.SEnvole;

    [Header("Envol")]
    [Tooltip("Vitesse de l'envol (m/s).")]
    public float flySpeed = 7f;

    [Tooltip("Durée de l'envol avant que l'oiseau disparaisse (s).")]
    public float flyDuration = 4f;

    [Tooltip("Nom du booléen de vol dans l'Animator de l'oiseau (\"flying\" pour le pack living birds).")]
    public string flyingAnimatorBool = "flying";

    [Header("Texte")]
    public bool showPopup = true;
    public string popupText = "+{0}\n<size=45%>OISEAU !</size>";
    public Color popupColor = new Color(1f, 0.84f, 0f);
    public float popupSize = 0.7f;
    public float popupDuration = 1.8f;

    bool hit;
    Renderer[] renderers;
    Animator animator;

    /// <summary>Vrai une fois touché (il s'envole et ne compte plus).</summary>
    public bool IsHit => hit;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        animator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        hit = false;
        if (!all.Contains(this)) all.Add(this);
    }

    void OnDisable() => all.Remove(this);

    /// <summary>Centre de l'oiseau (milieu de son modèle 3D).</summary>
    public Vector3 Center
    {
        get
        {
            bool found = false;
            Bounds b = default;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
            return found ? b.center : transform.position;
        }
    }

    /// <summary>
    /// Cherche l'oiseau le plus proche du début du segment [from, to] qui est traversé par ce segment.
    /// Utilisé par ArrowHit à chaque pas physique.
    /// </summary>
    public static bool FindHit(Vector3 from, Vector3 to, out BirdTarget bird, out Vector3 point, out float distance)
    {
        bird = null; point = default; distance = float.MaxValue;
        Vector3 seg = to - from;
        float len = seg.magnitude;
        if (len < 1e-5f) return false;
        Vector3 dir = seg / len;

        foreach (var b in all)
        {
            if (b == null || b.hit) continue;
            Vector3 c = b.Center;
            float t = Mathf.Clamp(Vector3.Dot(c - from, dir), 0f, len);   // point du segment le plus proche
            Vector3 p = from + dir * t;
            if ((c - p).sqrMagnitude > b.hitRadius * b.hitRadius) continue;
            if (t < distance) { bird = b; point = p; distance = t; }
        }
        return bird != null;
    }

    /// <summary>Appelé par la flèche qui touche l'oiseau.</summary>
    public void OnArrowHit(Vector3 point, Vector3 arrowDirection) => OnArrowHit(point, arrowDirection, false);

    public void OnArrowHit(Vector3 point, Vector3 arrowDirection, bool bonus)
    {
        if (hit) return;
        hit = true;

        if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterHit(points, false, bonus);
        else Debug.LogWarning("[BirdTarget] Pas de ScoreManager dans la scène : point non compté.");

        Debug.Log($"[BirdTarget] Oiseau '{name}' touché : +{points}", this);

        if (showPopup) StartCoroutine(Popup(Center));
        if (reaction == Reaction.Eclate) StartCoroutine(Burst());
        else StartCoroutine(FlyAway(arrowDirection));
    }

    IEnumerator FlyAway(Vector3 arrowDirection)
    {
        // On coupe les scripts du pack "living birds" (lb_...) pour qu'ils ne reprennent pas la main pendant l'envol
        var packScripts = new List<MonoBehaviour>();
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (mb == this || mb == null || !mb.enabled) continue;
            if (!mb.GetType().Name.StartsWith("lb_")) continue;
            mb.StopAllCoroutines();
            mb.enabled = false;
            packScripts.Add(mb);
        }

        // Ses colliders sont coupés pendant l'envol (les flèches ne rebondissent plus dessus)
        var colliders = new List<Collider>();
        foreach (var c in GetComponentsInChildren<Collider>())
            if (c.enabled) { c.enabled = false; colliders.Add(c); }

        // Si l'oiseau a un Rigidbody, on le pilote nous-mêmes
        var rb = GetComponent<Rigidbody>();
        bool rbWasKinematic = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;

        // Animation de vol
        if (animator != null && HasBool(animator, flyingAnimatorBool)) animator.SetBool(flyingAnimatorBool, true);

        // Direction : dans le sens de la flèche (il fuit), vers le haut
        Vector3 flat = Vector3.ProjectOnPlane(arrowDirection, Vector3.up);
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        flat.Normalize();
        flat = Quaternion.AngleAxis(Random.Range(-35f, 35f), Vector3.up) * flat;   // un peu de hasard
        Vector3 dir = (flat + Vector3.up * 0.8f).normalized;

        Quaternion targetRot = Quaternion.LookRotation(flat);

        for (float t = 0f; t < flyDuration; t += Time.deltaTime)
        {
            float speed = flySpeed * Mathf.Clamp01(0.3f + t * 1.5f);   // démarre doucement puis accélère
            transform.position += dir * speed * Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 8f * Time.deltaTime);
            yield return null;
        }

        // On remet tout comme avant puis on cache l'oiseau.
        // (Le BirdController du pack pourra le faire réapparaître ailleurs.)
        if (animator != null && HasBool(animator, flyingAnimatorBool)) animator.SetBool(flyingAnimatorBool, false);
        if (rb != null) rb.isKinematic = rbWasKinematic;
        foreach (var mb in packScripts) if (mb != null) mb.enabled = true;
        foreach (var c in colliders) if (c != null) c.enabled = true;

        gameObject.SetActive(false);
    }

    IEnumerator Burst()
    {
        // Petit "pop" : gonfle puis disparaît
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        Vector3 s0 = transform.localScale;
        for (float k = 0f; k < 1f; k += Time.deltaTime / 0.15f)
        {
            transform.localScale = s0 * (k < 0.5f ? Mathf.Lerp(1f, 1.4f, k * 2f) : Mathf.Lerp(1.4f, 0.05f, (k - 0.5f) * 2f));
            yield return null;
        }
        // Remis en réserve (désactivé) plutôt que détruit : l'objet reste dans la scène
        transform.localScale = s0;
        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
        gameObject.SetActive(false);
    }

    static bool HasBool(Animator a, string name)
    {
        if (string.IsNullOrEmpty(name) || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name) return true;
        return false;
    }

    static IEnumerator PopupRoutine(Vector3 start, string text, Color color, float size, float duration)
    {
        var cam = Camera.main;
        float distance = cam ? Vector3.Distance(cam.transform.position, start) : 10f;
        float baseSize = Mathf.Max(2f, distance * size);

        var go = new GameObject("Popup oiseau");
        go.transform.position = start;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);

        for (float t = 0f; t < duration; t += Time.deltaTime)
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
        Destroy(go);
    }

    IEnumerator Popup(Vector3 at)
    {
        // Le texte reste sur place même si l'oiseau s'en va : on le lance sur le ScoreManager (toujours actif)
        var host = ScoreManager.Instance != null ? (MonoBehaviour)ScoreManager.Instance : this;
        host.StartCoroutine(PopupRoutine(at + Vector3.up * 0.4f, string.Format(popupText, points), popupColor, popupSize, popupDuration));
        yield break;
    }

    // Montre la zone touchable dans la vue Scene quand l'oiseau est sélectionné
    void OnDrawGizmosSelected()
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        Gizmos.color = new Color(1f, 0.84f, 0f, 0.6f);
        Gizmos.DrawWireSphere(Center, hitRadius);
    }
}
