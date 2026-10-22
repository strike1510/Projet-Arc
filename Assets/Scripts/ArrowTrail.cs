using UnityEngine;

/// <summary>
/// Traînée de la flèche en vol. Le TrailRenderer est un vrai composant posé sur l'enfant "Traînée" de la flèche
/// (Projet Arc > Configurer le projet). Ce script l'allume au tir et l'éteint à l'impact.
/// Les couleurs, la largeur et la durée viennent de l'ARC qui tire (chaque arc a sa traînée),
/// sinon du style par défaut ci-dessous.
/// </summary>
[RequireComponent(typeof(ArrowHit))]
public class ArrowTrail : MonoBehaviour
{
    [Tooltip("Le TrailRenderer de la flèche (enfant \"Traînée\").")]
    public TrailRenderer trail;

    [Tooltip("Particle System d'étincelles de la flèche (allumé seulement si l'arc a \"Trail Sparks\").")]
    public ParticleSystem sparks;

    [Header("Style par défaut (tir sans arc)")]
    public Gradient defaultColors;
    public float defaultWidth = 0.03f;
    public float defaultTime = 0.3f;

    ArrowHit hit;
    bool flicker;
    float baseWidth;
    Material defaultMaterial;

    void Awake()
    {
        hit = GetComponent<ArrowHit>();
        hit.FlightStarted += OnFlightStarted;
        SetSparks(false);
        if (trail != null)
        {
            defaultMaterial = trail.sharedMaterial;
            trail.emitting = false;
            trail.Clear();
        }
    }

    void OnDestroy()
    {
        if (hit != null) hit.FlightStarted -= OnFlightStarted;
    }

    void SetSparks(bool on)
    {
        if (sparks == null) return;
        var em = sparks.emission;
        em.enabled = on;
        if (on && !sparks.isPlaying) sparks.Play();
    }

    void OnDisable()
    {
        SetSparks(false);
        if (trail == null) return;
        trail.emitting = false;
        trail.Clear();
    }

    void OnFlightStarted(ArrowHit arrow, BowController bow)
    {
        if (trail == null) return;

        bool fromBow = bow != null && bow.trailColors != null && bow.trailColors.colorKeys.Length > 0;
        trail.colorGradient = fromBow ? bow.trailColors : (defaultColors ?? trail.colorGradient);
        baseWidth = fromBow ? bow.trailWidth : defaultWidth;
        trail.time = fromBow ? bow.trailTime : defaultTime;
        flicker = fromBow && bow.trailFlicker;
        trail.widthMultiplier = baseWidth;
        trail.sharedMaterial = fromBow && bow.trailMaterial != null ? bow.trailMaterial : defaultMaterial;
        SetSparks(fromBow && bow.trailSparks);

        trail.Clear();
        trail.emitting = true;
    }

    void LateUpdate()
    {
        if (trail == null || !trail.emitting) return;

        // Impact (ou flèche rattrapée) : la traînée s'arrête et s'efface toute seule
        if (!hit.IsFlying)
        {
            SetSparks(false);
            trail.emitting = false;
            trail.widthMultiplier = baseWidth;
            return;
        }

        if (flicker) trail.widthMultiplier = baseWidth * Random.Range(0.35f, 1.35f);
    }
}
