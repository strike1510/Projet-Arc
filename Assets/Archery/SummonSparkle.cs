using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Petite gerbe d'étincelles quand un arc du sélecteur apparaît ou disparaît.
/// Texture et matériau générés par code (shader "Sprites/Default", inclus d'office dans les builds).
/// </summary>
public static class SummonSparkle
{
    static Material material;

    static Material Material
    {
        get
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = new Material(shader) { mainTexture = SoftDot(32) };
            return material;
        }
    }

    public static void Play(Vector3 position, Color color, int count = 28)
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
        r.material = Material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
    }

    static Texture2D SoftDot(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
            px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}