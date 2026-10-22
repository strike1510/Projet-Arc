using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Aide à la visée : pendant qu'on tend la corde, un trait montre la trajectoire que prendra la flèche
/// (vitesse selon la tension, chute avec la gravité), jusqu'au premier obstacle, avec un point d'impact.
/// Pour le Bow_Monkey, deux traits de plus montrent les flèches latérales du tir triple.
///
/// Les traits (LineRenderer) et le point d'impact sont de vrais objets, enfants "Aide à la visée" de l'arc
/// (Projet Arc > Configurer le projet). Ils sont cachés quand aucune flèche n'est encochée.
/// </summary>
[RequireComponent(typeof(BowController))]
public class AimGuide : MonoBehaviour
{
    [Tooltip("Trait de la flèche du centre.")]
    public LineRenderer line;
    [Tooltip("Traits des flèches latérales (tir triple). Vides si l'arc ne tire qu'une flèche.")]
    public LineRenderer leftLine, rightLine;
    [Tooltip("Petit repère posé là où la flèche du centre touchera.")]
    public Transform impactMarker;

    [Tooltip("Durée de vol simulée au maximum (s).")]
    public float maxTime = 2.5f;
    [Tooltip("Pas de la simulation (s). Plus petit = courbe plus fine.")]
    public float timeStep = 0.025f;
    [Tooltip("Tension minimum avant d'afficher le trait (0 à 1).")]
    [Range(0f, 1f)] public float minDraw = 0.05f;

    BowController bow;
    TripleShot triple;
    readonly Vector3[] points = new Vector3[256];
    readonly RaycastHit[] hits = new RaycastHit[16];

    void Awake()
    {
        bow = GetComponent<BowController>();
        triple = GetComponent<TripleShot>();
        Hide();
    }

    void OnDisable() => Hide();

    void Hide()
    {
        if (line != null) line.enabled = false;
        if (leftLine != null) leftLine.enabled = false;
        if (rightLine != null) rightLine.enabled = false;
        if (impactMarker != null) impactMarker.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var arrow = bow.nockedArrow;
        if (arrow == null || !arrow.IsHeld || bow.arrowNock == null || bow.maxDraw <= 0f || arrow.Draw / bow.maxDraw < minDraw)
        {
            Hide();
            return;
        }

        Vector3 dir = bow.arrowNock.forward;
        float speed = arrow.LaunchSpeed;
        Vector3 g = bow.noGravity ? Vector3.zero : Physics.gravity;

        // La physique déplace le pivot de la flèche (son Rigidbody) ; ArrowHit garde la flèche alignée sur sa vitesse.
        // On simule donc le pivot, et la pointe = pivot + décalage le long de la flèche, comme pendant le vol.
        Vector3 pivot = arrow.transform.position;
        Vector3 tip = arrow.tip != null ? arrow.tip.position : pivot;
        Vector3 axis = arrow.tip != null && arrow.grabPoint != null ? (arrow.tip.position - arrow.grabPoint.position).normalized : dir;
        float along = Vector3.Dot(tip - pivot, axis);
        Vector3 perp = (tip - pivot) - axis * along;

        bool hit = Draw(line, pivot, along, perp, dir * speed, g, out Vector3 end, out Vector3 normal);
        if (impactMarker != null)
        {
            impactMarker.gameObject.SetActive(hit);
            if (hit)
            {
                impactMarker.position = end + normal * 0.01f;
                impactMarker.rotation = Quaternion.LookRotation(-normal);
            }
        }

        // Tir triple : mêmes écarts que TripleShot
        if (triple != null && (leftLine != null || rightLine != null))
        {
            Vector3 up = Vector3.ProjectOnPlane(bow.arrowNock.up, dir);
            if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.up, dir);
            up.Normalize();
            Vector3 right = Vector3.Cross(up, dir).normalized;
            float sideSpeed = speed * triple.speedFactor;
            if (leftLine != null) Draw(leftLine, pivot - right * triple.sideOffset, along, perp, Quaternion.AngleAxis(-triple.spreadAngle, up) * dir * sideSpeed, g, out _, out _);
            if (rightLine != null) Draw(rightLine, pivot + right * triple.sideOffset, along, perp, Quaternion.AngleAxis(triple.spreadAngle, up) * dir * sideSpeed, g, out _, out _);
        }
    }

    /// <summary>
    /// Même calcul que le moteur physique (pas fixe Time.fixedDeltaTime, vitesse puis position),
    /// pour que le trait tombe exactement là où la flèche ira. Renvoie true si la courbe touche quelque chose.
    /// </summary>
    bool Draw(LineRenderer lr, Vector3 pivot, float along, Vector3 perp, Vector3 v, Vector3 g, out Vector3 end, out Vector3 normal)
    {
        normal = Vector3.up;
        Vector3 tip = pivot + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward) * along + perp;
        end = tip;
        if (lr == null) return false;

        float dt = Time.fixedDeltaTime;
        int count = 0;
        points[count++] = tip;
        bool touched = false;
        int steps = Mathf.CeilToInt(maxTime / dt);
        int every = Mathf.Max(Mathf.Max(1, Mathf.RoundToInt(timeStep / dt)), Mathf.CeilToInt(steps / (points.Length - 4f)));   // un point du trait tous les 'every' pas

        Vector3 p = pivot;
        Vector3 lastDrawn = tip;
        for (int i = 1; i <= steps; i++)
        {
            v += g * dt;          // comme PhysX : la vitesse d'abord...
            p += v * dt;          // ...puis la position
            Vector3 next = p + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward) * along + perp;

            if (Cast(tip, next, out RaycastHit h))
            {
                points[count++] = h.point;
                end = h.point; normal = h.normal;
                touched = true;
                break;
            }
            tip = next;
            end = tip;
            if (i % every == 0 && count < points.Length - 2) { points[count++] = tip; lastDrawn = tip; }
        }
        if (!touched && end != lastDrawn && count < points.Length) points[count++] = end;

        lr.positionCount = count;
        for (int i = 0; i < count; i++) lr.SetPosition(i, points[i]);
        lr.enabled = true;
        return touched;
    }

    bool Cast(Vector3 from, Vector3 to, out RaycastHit best)
    {
        best = default;
        Vector3 seg = to - from;
        float len = seg.magnitude;
        if (len < 1e-5f) return false;

        int n = Physics.RaycastNonAlloc(from, seg / len, hits, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;                       // l'arc
            if (c.GetComponentInParent<ArrowHit>() != null) continue;             // les flèches
            if (c.GetComponentInParent<XROrigin>() != null) continue;             // le joueur
            if (!found || hits[i].distance < best.distance) { best = hits[i]; found = true; }
        }
        return found;
    }
}
