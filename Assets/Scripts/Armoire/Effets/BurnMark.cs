using UnityEngine;

/// <summary>
/// Trace de brûlure laissée par la foudre : éclair de lumière, braise orange qui s'éteint,
/// tache noire qui s'efface. Tout disparaît au bout de 'duration' secondes.
/// </summary>
public class BurnMark : MonoBehaviour
{
    public float duration = 2f;
    public float size = 0.2f;

    SpriteRenderer charMark, ember;
    Light flash;
    float t;

    /// <summary>Cherche la surface touchée devant la pointe et y pose une brûlure.</summary>
    public static void SpawnAt(Vector3 tipPosition, Vector3 direction, Transform ignore, float duration)
    {
        Vector3 start = tipPosition - direction * 0.4f;
        bool found = false;
        RaycastHit best = default;
        foreach (var h in Physics.RaycastAll(start, direction, 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            if (h.collider.GetComponentInParent<ArrowController>() != null) continue;
            if (found && h.distance >= best.distance) continue;
            best = h;
            found = true;
        }
        if (!found) return;

        var go = new GameObject("Brûlure");
        go.transform.SetPositionAndRotation(best.point + best.normal * 0.004f, Quaternion.LookRotation(-best.normal));
        var target = best.collider.GetComponentInParent<Target>();
        if (target != null) go.transform.SetParent(target.transform, true);   // suit la cible si elle bouge
        go.AddComponent<BurnMark>().duration = duration;
    }

    void Start()
    {
        charMark = NewSprite("Tache", EffectAssets.BurnSprite, size, Color.white, 0);
        charMark.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        ember = NewSprite("Braise", EffectAssets.GlowSprite, size * 1.4f, new Color(1f, 0.5f, 0.12f, 0.9f), 1);

        flash = new GameObject("Flash").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.transform.localPosition = new Vector3(0f, 0f, -0.1f);
        flash.type = LightType.Point;
        flash.color = new Color(0.6f, 0.85f, 1f);
        flash.range = 5f;
        flash.intensity = 6f;
        flash.shadows = LightShadows.None;
    }

    SpriteRenderer NewSprite(string name, Sprite sprite, float diameter, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * diameter;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        return sr;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / Mathf.Max(0.01f, duration);

        // Flash de lumière très court
        if (flash != null)
        {
            flash.intensity = Mathf.Lerp(6f, 0f, t / 0.25f);
            if (t > 0.25f) Destroy(flash.gameObject);
        }

        // La braise s'éteint dans le premier tiers
        var e = ember.color;
        e.a = 0.9f * Mathf.Clamp01(1f - k / 0.35f);
        ember.color = e;

        // La tache reste puis s'efface sur la deuxième moitié
        var c = charMark.color;
        c.a = Mathf.Clamp01((1f - k) / 0.5f);
        charMark.color = c;

        if (k >= 1f) Destroy(gameObject);
    }
}
