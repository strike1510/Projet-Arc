using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Arc Bio : traînée discrète de feuilles et de spores derrière la flèche.
/// Les particules tombent doucement et disparaissent au bout d'environ 5 secondes.
/// Ajouté par BowEffects au moment du tir, se retire tout seul à la fin du vol.
/// </summary>
public class LeafTrail : MonoBehaviour
{
    [Tooltip("Feuilles par mètre parcouru.")]
    public float leavesPerMeter = 2f;

    [Tooltip("Spores par mètre parcouru.")]
    public float sporesPerMeter = 5f;

    public float lifetime = 5f;

    FlightWatch watch;
    Transform follow;
    Transform holder;
    ParticleSystem leaves, spores;

    public void Init(Transform tip, Transform back)
    {
        // On émet un peu en arrière de la pointe, au milieu de la flèche
        follow = back != null ? back : tip;
        watch = new FlightWatch(gameObject, tip, tip.position - (back != null ? back.position : transform.position));

        // Les particules ne sont pas enfants de la flèche : elles restent en l'air après l'impact
        holder = new GameObject("Traînée de feuilles").transform;
        holder.position = follow.position;

        leaves = MakeSystem("Feuilles", EffectAssets.LeafMaterial, leavesPerMeter,
            new Vector2(0.025f, 0.045f), new Color(0.33f, 0.62f, 0.2f, 0.9f), new Color(0.62f, 0.8f, 0.25f, 0.9f), 0.03f);
        spores = MakeSystem("Spores", EffectAssets.SporeMaterial, sporesPerMeter,
            new Vector2(0.006f, 0.012f), new Color(0.85f, 0.95f, 0.6f, 0.7f), new Color(1f, 1f, 0.85f, 0.45f), 0.005f);
    }

    ParticleSystem MakeSystem(string name, Material mat, float perMeter, Vector2 size, Color a, Color b, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(holder, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.85f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = perMeter;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.02f;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

        // Apparaît vite, reste, puis s'efface sur la fin
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.05f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        color.color = g;

        // Petit flottement dans l'air
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.3f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    void LateUpdate()
    {
        if (watch == null) return;
        if (follow != null) holder.position = follow.position;
        if (watch.Tick(Time.deltaTime, out _)) Destroy(this);
    }

    void OnDestroy()
    {
        // Fin du vol (ou flèche détruite) : on arrête d'émettre, les feuilles déjà là finissent de tomber
        if (holder == null) return;
        if (leaves != null) leaves.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (spores != null) spores.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(holder.gameObject, lifetime + 0.5f);
    }
}
