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
// S'exécute après ArrowController : son LateUpdate remet la gravité aux flèches lâchées,
// le nôtre (juste après) garde figée une flèche que NOUS avons plantée.
[DefaultExecutionOrder(100)]
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
    bool counted;          // ce tir compte pour le score (false si la manche est finie)
    bool grabbedWhileStuck;
    bool wasMoving;        // la flèche a déjà bougé depuis le tir (physique active)
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

    void LateUpdate()
    {
        // Une flèche plantée par notre raycast reste figée (ArrowController ne connaît que ses propres impacts)
        if (stuck && !grab.isSelected && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    void FixedUpdate()
    {
        if (!flying) return;

        if (rb.isKinematic)
        {
            // Figée en plein vol par un autre script (ex. collision dans ArrowController) sans qu'on ait vu l'impact :
            // on termine le tir pour ne pas bloquer la manche.
            if (wasMoving && !grab.isSelected) EndFlight(null);
            return;
        }
        wasMoving = true;

        flightTime += Time.fixedDeltaTime;
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (flightTime > maxFlightTime || transform.position.y < -50f)
        {
            Debug.Log("[ArrowHit] Flèche perdue (rien touché).");
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

        // Trajectoire visible dans la vue Scene pendant 5 s (jaune = vol)
        Debug.DrawLine(previousTip, end, Color.yellow, 5f);
        previousTip = tip;

        if (closest.HasValue) StickInto(closest.Value, dir);
    }

    /// <summary>
    /// Pour tester sans casque : lance la flèche depuis 'origin' dans la direction 'dir'.
    /// Utilisé par TestShooter.
    /// </summary>
    public void DebugLaunch(Vector3 origin, Vector3 dir, float speed)
    {
        if (grab.isSelected) return;
        if (flying) EndFlight(null);   // relancée avant d'avoir touché : le tir précédent compte comme raté

        transform.SetParent(null, true);
        rb.isKinematic = false;
        rb.useGravity = true;

        Vector3 axis = (arrow.tip.position - arrow.grabPoint.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, dir) * transform.rotation;
        transform.position += origin - arrow.grabPoint.position;

        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = dir * speed;
        StartFlight();
    }

    void StartFlight()
    {
        flying = true;
        stuck = false;
        wasMoving = false;
        flightTime = 0f;
        previousTip = arrow.tip.position;
        transform.SetParent(null, true);

        counted = ScoreManager.Instance == null || ScoreManager.Instance.RegisterShot();
        if (!counted) Debug.Log("[ArrowHit] Manche terminée : ce tir ne compte pas.");
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
        if (target == null) Debug.Log($"[ArrowHit] Flèche plantée dans '{hit.collider.name}' (pas une cible).", hit.collider);
        Debug.DrawLine(hit.point - dir * 0.3f, hit.point, target != null ? Color.green : Color.red, 10f);
        EndFlight(target, hit.point);
    }

    void EndFlight(Target target, Vector3 point = default)
    {
        flying = false;

        if (!counted) return;
        counted = false;

        if (target != null) target.OnArrowHit(point);
        else if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss();
    }

    /// <summary>
    /// Filet de sécurité : si la physique fait toucher la flèche avant que le raycast ne voie l'impact,
    /// on compte l'impact au point de contact.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (!flying || grab.isSelected) return;
        if (collision.transform.IsChildOf(transform)) return;
        if (arrow.bow != null && collision.transform.IsChildOf(arrow.bow.transform)) return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : arrow.tip.position;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        var target = collision.collider.GetComponentInParent<Target>();
        if (target != null) transform.SetParent(target.transform, true);

        stuck = true;
        if (target == null) Debug.Log($"[ArrowHit] Flèche arrêtée par '{collision.collider.name}' (pas une cible).", collision.collider);
        EndFlight(target, point);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        // Le joueur reprend la flèche (plantée ou en vol) : elle n'est plus en vol.
        grabbedWhileStuck = stuck || rb.isKinematic;
        if (flying) EndFlight(null);   // rattrapée en plein vol : compte comme ratée
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
