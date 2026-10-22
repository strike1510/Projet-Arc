using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Textures et matériaux des effets, générés par code (aucun fichier à importer).
/// Shader "Sprites/Default" : inclus d'office dans les builds, compatible URP.
/// </summary>
public static class EffectAssets
{
    static Material leafMat, sporeMat, lineMat;
    static Sprite burnSprite, glowSprite;

    static Shader BaseShader
    {
        get
        {
            var s = Shader.Find("Sprites/Default");
            return s != null ? s : Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }
    }

    public static Material LeafMaterial => leafMat != null ? leafMat : leafMat = new Material(BaseShader) { mainTexture = LeafTexture(64) };
    public static Material SporeMaterial => sporeMat != null ? sporeMat : sporeMat = new Material(BaseShader) { mainTexture = SoftDot(32, 2f) };
    public static Material LineMaterial => lineMat != null ? lineMat : lineMat = new Material(BaseShader);

    public static Sprite BurnSprite => burnSprite != null ? burnSprite : burnSprite = ToSprite(BurnTexture(128));
    public static Sprite GlowSprite => glowSprite != null ? glowSprite : glowSprite = ToSprite(SoftDot(64, 1.5f));

    /// <summary>Sprite de 1 m de large : on règle sa taille avec le scale.</summary>
    static Sprite ToSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);

    public static LineRenderer NewLine(Transform parent, string name, float startWidth, float endWidth,
        Color startColor, Color endColor, int points, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points;
        line.widthMultiplier = 1f;
        line.widthCurve = AnimationCurve.Linear(0f, startWidth, 1f, endWidth);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                  new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(endColor.a, 1f) });
        line.colorGradient = g;
        line.material = LineMaterial;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        return line;
    }

    /// <summary>Petite gerbe d'étincelles (apparition / disparition d'un arc).</summary>
    public static void Sparkle(Vector3 position, Color color, int count = 28)
    {
        var go = new GameObject("Étincelles");
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.gravityModifier = -0.05f;   // monte doucement
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        fade.color = g;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SporeMaterial;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
    }

    // ---------- Textures ----------

    /// <summary>Feuille blanche (teintée par la couleur des particules), avec une nervure.</summary>
    static Texture2D LeafTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (y + 0.5f) / size * 2f - 1f;          // le long de la feuille
            float v = (x + 0.5f) / size * 2f - 1f;          // en travers
            float halfWidth = 0.42f * Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.8f);
            float edge = Mathf.Clamp01((halfWidth - Mathf.Abs(v)) * size * 0.5f);
            float vein = Mathf.Abs(v) < 0.035f && u > -0.9f && u < 0.85f ? 0.7f : 1f;
            float shade = 0.85f + 0.15f * (1f - Mathf.Abs(v) / Mathf.Max(halfWidth, 0.01f));
            float c = vein * shade;
            px[y * size + x] = new Color(c, c, c, edge);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>Point lumineux flou (spores, lueur de braise).</summary>
    static Texture2D SoftDot(int size, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), falloff));
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>Tache de brûlure : centre charbonneux, bord irrégulier.</summary>
    static Texture2D BurnTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        var dark = new Color(0.04f, 0.03f, 0.02f);
        var brown = new Color(0.22f, 0.12f, 0.05f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
            float n = Mathf.PerlinNoise(x * 0.07f + 3.1f, y * 0.07f + 7.7f) - 0.5f;
            float e = d + n * 0.4f;
            float a = Mathf.Pow(Mathf.Clamp01((1f - e) / 0.5f), 1.3f) * 0.9f;
            float speck = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
            var c = Color.Lerp(dark, brown, Mathf.Clamp01(e * 0.9f + speck * 0.2f));
            c.a = a;
            px[y * size + x] = c;
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
