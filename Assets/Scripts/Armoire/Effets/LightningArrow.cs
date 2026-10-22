using UnityEngine;

/// <summary>
/// Arc Fulminaleuse : pendant le vol, la flèche disparaît et devient un éclair qui crépite.
/// À l'impact, elle réapparaît plantée et laisse une brûlure (BurnMark).
/// Ajouté par BowEffects au moment du tir, se retire tout seul à la fin du vol.
/// </summary>
public class LightningArrow : MonoBehaviour
{
    const int Points = 14;

    public float boltLength = 1.4f;
    public float jitter = 0.06f;
    public Color coreColor = new Color(0.9f, 0.97f, 1f, 1f);
    public Color glowColor = new Color(0.35f, 0.65f, 1f, 0.3f);

    FlightWatch watch;
    Transform tip;
    float burnDuration;
    Renderer[] hidden;
    GameObject holder;
    LineRenderer core, glow;
    Light glowLight;
    readonly Vector2[] offsets = new Vector2[Points];
    readonly Vector3[] points = new Vector3[Points];
    float nextJitter;

    public void Init(Transform arrowTip, Transform back, float burn)
    {
        tip = arrowTip;
        burnDuration = burn;
        watch = new FlightWatch(gameObject, tip, tip.position - back.position);

        // La flèche devient invisible : seul l'éclair se voit
        hidden = System.Array.FindAll(GetComponentsInChildren<Renderer>(), r => r.enabled);
        foreach (var r in hidden) r.enabled = false;

        holder = new GameObject("Éclair");
        var fade = glowColor;
        fade.a = 0f;
        glow = EffectAssets.NewLine(holder.transform, "Halo", 0.10f, 0.03f, glowColor, fade, Points, 0);
        var coreEnd = new Color(0.45f, 0.75f, 1f, 0f);
        core = EffectAssets.NewLine(holder.transform, "Coeur", 0.022f, 0.004f, coreColor, coreEnd, Points, 1);

        glowLight = new GameObject("Lumière").AddComponent<Light>();
        glowLight.transform.SetParent(holder.transform, false);
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.55f, 0.8f, 1f);
        glowLight.range = 4f;
        glowLight.shadows = LightShadows.None;

        Rejitter();
        UpdateBolt();
    }

    void LateUpdate()
    {
        if (watch == null) return;
        if (watch.Tick(Time.deltaTime, out bool grabbed))
        {
            End(!grabbed);
            return;
        }
        if (Time.time >= nextJitter) Rejitter();
        UpdateBolt();
    }

    void Rejitter()
    {
        nextJitter = Time.time + 0.04f;   // l'éclair change de forme ~25 fois par seconde
        for (int i = 0; i < Points; i++)
            offsets[i] = i == 0 ? Vector2.zero : Random.insideUnitCircle * jitter;
    }

    void UpdateBolt()
    {
        if (tip == null) return;
        Vector3 dir = watch.Direction;
        Vector3 head = tip.position;
        float length = Mathf.Min(boltLength, watch.Travelled + 0.15f);   // l'éclair s'allonge en sortant de l'arc

        Vector3 side = Vector3.Cross(dir, Vector3.up);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(dir, Vector3.right);
        side.Normalize();
        Vector3 up = Vector3.Cross(side, dir);

        for (int i = 0; i < Points; i++)
        {
            float k = i / (Points - 1f);
            points[i] = head - dir * (length * k) + (side * offsets[i].x + up * offsets[i].y) * (0.4f + 0.6f * k);
        }
        core.SetPositions(points);
        glow.SetPositions(points);

        glowLight.transform.position = head;
        glowLight.intensity = Random.Range(1.5f, 4f);
    }

    void End(bool impact)
    {
        Restore();
        if (impact && tip != null) BurnMark.SpawnAt(tip.position, watch.Direction, transform, burnDuration);
        Destroy(this);
    }

    void Restore()
    {
        if (hidden != null)
            foreach (var r in hidden)
                if (r != null) r.enabled = true;
        hidden = null;
        if (holder != null) Destroy(holder);
    }

    void OnDestroy() => Restore();
}
