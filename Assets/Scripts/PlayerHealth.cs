using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Points de vie du joueur dans l'Open World. À mettre sur le XR Origin.
/// Un shuriken retire 1 PV (écran rouge un instant). À 0 PV : retour au point de départ avec tous ses PV.
/// La vie s'affiche en bas du champ de vision ("PV 5/5").
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Tooltip("Points de vie au départ.")]
    public int maxHealth = 5;

    [Tooltip("Invincible pendant ce temps après un coup (s).")]
    public float invincibleTime = 1f;

    [Tooltip("Point de retour à 0 PV. Vide = là où le joueur a commencé.")]
    public Transform respawnPoint;

    [Tooltip("Récupère 1 PV toutes les X secondes sans être touché. 0 = jamais.")]
    public float regenEvery = 15f;

    [Tooltip("Afficher les PV en bas de la vue.")]
    public bool showHealth = true;

    public int Health { get; private set; }

    Camera cam;
    Vector3 startPos;
    Quaternion startRot;
    float lastHit = -999f, lastRegen;
    SpriteRenderer flash;
    TextMeshPro hud;

    void Awake()
    {
        Instance = this;
        Health = maxHealth;
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        cam = TargetSpawner.FindGameViewCamera();
        if (cam == null) return;

        // Voile rouge juste devant les yeux (invisible au repos)
        var f = new GameObject("FlashDegats");
        f.transform.SetParent(cam.transform, false);
        f.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.05f);
        f.transform.localScale = Vector3.one * 2f;
        flash = f.AddComponent<SpriteRenderer>();
        var tex = Texture2D.whiteTexture;
        flash.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        flash.color = new Color(1f, 0f, 0f, 0f);
        flash.sortingOrder = 1000;

        if (showHealth)
        {
            var h = new GameObject("PV");
            h.transform.SetParent(cam.transform, false);
            h.transform.localPosition = new Vector3(0f, -0.28f, 0.8f);
            hud = h.AddComponent<TextMeshPro>();
            hud.fontSize = 0.6f;
            hud.alignment = TextAlignmentOptions.Center;
            hud.textWrappingMode = TextWrappingModes.NoWrap;
            hud.outlineWidth = 0.25f;
            hud.outlineColor = new Color32(0, 0, 0, 255);
            RefreshHud();
        }
    }

    void Update()
    {
        if (regenEvery > 0f && Health < maxHealth && Time.time - lastHit > regenEvery && Time.time - lastRegen > regenEvery)
        {
            Health++;
            lastRegen = Time.time;
            RefreshHud();
        }
    }

    /// <summary>Vrai si le segment [a, b] passe à moins de 'radius' du corps du joueur (de la tête jusqu'au ventre).</summary>
    public bool IsHitBySegment(Vector3 a, Vector3 b, float radius)
    {
        if (cam == null) cam = TargetSpawner.FindGameViewCamera();
        if (cam == null) return false;
        Vector3 head = cam.transform.position;
        Vector3 belly = head + Vector3.down * 0.8f;
        return SegmentDistance(a, b, head, belly) < radius + 0.15f;
    }

    public void TakeDamage(int amount, Vector3 from)
    {
        if (Time.time - lastHit < invincibleTime) return;
        lastHit = Time.time;
        Health = Mathf.Max(0, Health - amount);
        RefreshHud();
        StopCoroutine(nameof(Flash));
        StartCoroutine(nameof(Flash));
        Debug.Log($"[PlayerHealth] Touché ! PV : {Health}/{maxHealth}");

        if (Health == 0) StartCoroutine(Respawn());
    }

    IEnumerator Flash()
    {
        if (flash == null) yield break;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            flash.color = new Color(1f, 0f, 0f, Mathf.Lerp(0.45f, 0f, t / 0.5f));
            yield return null;
        }
        flash.color = new Color(1f, 0f, 0f, 0f);
    }

    IEnumerator Respawn()
    {
        if (hud != null) hud.text = "<color=#FF5555>K.O. !</color>  Retour au départ…";
        yield return new WaitForSeconds(1.5f);

        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPos;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRot;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.SetPositionAndRotation(pos, rot);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();

        Health = maxHealth;
        lastHit = Time.time;
        RefreshHud();
    }

    void RefreshHud()
    {
        if (hud == null) return;
        float k = (float)Health / maxHealth;
        string color = k > 0.5f ? "#7CFF7C" : k > 0.25f ? "#FFD24A" : "#FF5555";
        hud.text = $"<color={color}>PV {Health}/{maxHealth}</color>";
    }

    static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
    {
        // Distance minimale entre deux segments (pour savoir si le shuriken frôle le joueur)
        Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        if (a <= 1e-6f && e <= 1e-6f) return Vector3.Distance(p1, p2);
        if (a <= 1e-6f) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 1e-6f) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2), denom = a * e - b * b;
                s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
    }
}
