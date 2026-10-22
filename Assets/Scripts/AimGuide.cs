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
        Vector3 start = arrow.tip != null ? arrow.tip.position : arrow.transform.position;

        bool hit = Draw(line, start, dir * speed, out Vector3 end, out Vector3 normal);
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
            if (leftLine != null) Draw(leftLine, start - right * triple.sideOffset, Quaternion.AngleAxis(-triple.spreadAngle, up) * dir * sideSpeed, out _, out _);
            if (rightLine != null) Draw(rightLine, start + right * triple.sideOffset, Quaternion.AngleAxis(triple.spreadAngle, up) * dir * sideSpeed, out _, out _);
        }
    }

    /// <summary>Calcule la courbe et la met dans le trait. Renvoie true si elle touche quelque chose.</summary>
    bool Draw(LineRenderer lr, Vector3 p, Vector3 v, out Vector3 end, out Vector3 normal)
    {
        end = p; normal = Vector3.up;
        if (lr == null) return false;

        int count = 0;
        points[count++] = p;
        bool touched = false;
        Vector3 g = Physics.gravity;
        int steps = Mathf.Min(points.Length - 1, Mathf.CeilToInt(maxTime / Mathf.Max(0.005f, timeStep)));

        for (int i = 0; i < steps; i++)
        {
            Vector3 next = p + v * timeStep + 0.5f * g * timeStep * timeStep;
            v += g * timeStep;

            if (Cast(p, next, out RaycastHit h))
            {
                points[count++] = h.point;
                end = h.point; normal = h.normal;
                touched = true;
                break;
            }
            points[count++] = next;
            p = next;
            end = p;
        }

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
