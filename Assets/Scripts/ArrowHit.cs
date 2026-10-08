using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// À ajouter sur la flèche, à côté d'ArrowController (on ne modifie pas ArrowController).
/// - détecte le tir (la flèche quitte l'arc)
/// - oriente la flèche dans le sens du vol
/// - détecte l'impact par raycast (fiable même à 30 m/s), plante la flèche et donne les points
/// </summary>
[RequireComponent(typeof(ArrowController))]
public class ArrowHit : MonoBehaviour
{
    [Tooltip("Profondeur à laquelle la pointe s'enfonce dans la cible.")]
    public float penetration = 0.05f;

    [Tooltip("Au-delà de ce temps sans rien toucher, la flèche compte comme ratée.")]
    public float maxFlightTime = 8f;

    ArrowController arrow;
    Rigidbody rb;
    XRGrabInteractable grab;

    bool wasNocked;        // était encochée à la frame précédente
    bool flying;           // tirée et pas encore arrêtée
    bool stuck;            // plantée quelque part
    bool grabbedWhileStuck;
    float flightTime;
    Vector3 previousTip;

    void Awake()
    {
        arrow = GetComponent<ArrowController>();
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    void OnDestroy()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    void Update()
    {
        // Tir détecté : la flèche était encochée sur l'arc et ne l'est plus, et la main l'a lâchée.
        bool nocked = arrow.bow != null && arrow.bow.nockedArrow == arrow;
        if (wasNocked && !nocked && !grab.isSelected) StartFlight();
        wasNocked = nocked;
    }

    void FixedUpdate()
    {
        if (!flying || rb.isKinematic) return;

        flightTime += Time.fixedDeltaTime;
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (flightTime > maxFlightTime || transform.position.y < -50f)
        {
            EndFlight(null);
            return;
        }

        if (speed < 0.5f)
        {
            // Une fois que la flèche ralentit vraiment, elle est tombée : ratée.
            if (flightTime > 0.3f) EndFlight(null);
            return;
        }

        Vector3 dir = velocity / speed;

        // Oriente la flèche dans le sens du vol (la pointe suit la trajectoire)
        Vector3 axis = (arrow.tip.position - arrow.grabPoint.position).normalized;
        rb.MoveRotation(Quaternion.FromToRotation(axis, dir) * rb.rotation);

        // Raycast de la pointe précédente jusqu'à où elle sera au prochain pas physique
        Vector3 tip = arrow.tip.position;
        Vector3 end = tip + velocity * Time.fixedDeltaTime;
        Vector3 segment = end - previousTip;

        var hits = Physics.RaycastAll(previousTip, segment.normalized, segment.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        RaycastHit? closest = null;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;                    // la flèche elle-même
            if (arrow.bow != null && h.collider.transform.IsChildOf(arrow.bow.transform)) continue;    // l'arc
            if (closest == null || h.distance < closest.Value.distance) closest = h;
        }

        previousTip = tip;

        if (closest.HasValue) StickInto(closest.Value, dir);
    }

    void StartFlight()
    {
        flying = true;
        stuck = false;
        flightTime = 0f;
        previousTip = arrow.tip.position;
        transform.SetParent(null, true);

        if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterShot();
    }

    void StickInto(RaycastHit hit, Vector3 dir)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        // Place la pointe juste dans la surface touchée
        transform.position += (hit.point + dir * penetration) - arrow.tip.position;

        var target = hit.collider.GetComponentInParent<Target>();
        if (target != null) transform.SetParent(target.transform, true);   // suit la cible si elle bouge

        stuck = true;
        EndFlight(target, hit.point);
    }

    void EndFlight(Target target, Vector3 point = default)
    {
        flying = false;

        if (target != null) target.OnArrowHit(point);
        else if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss();
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        // Le joueur reprend la flèche (plantée ou en vol) : elle n'est plus en vol.
        grabbedWhileStuck = stuck || rb.isKinematic;
        flying = false;
        stuck = false;
        transform.SetParent(null, true);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        // XR Interaction Toolkit remet la physique comme au moment de la saisie.
        // Si on a repris une flèche plantée (donc figée), elle resterait figée en l'air une fois lâchée :
        // on réactive la physique juste après.
        if (grabbedWhileStuck) StartCoroutine(RestorePhysics());
        grabbedWhileStuck = false;
    }

    IEnumerator RestorePhysics()
    {
        yield return new WaitForFixedUpdate();
        if (stuck || grab.isSelected) yield break;
        rb.isKinematic = false;
        rb.useGravity = true;
    }
}
