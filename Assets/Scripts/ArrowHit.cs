using System;
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
    bool gravityWarned;
    Vector3 previousTip;
    Vector3 lastDir = Vector3.forward;   // dernière direction de vol connue
    bool bonus;            // flèche bonus (tir triple) : ne consomme pas de flèche de la manche

    /// <summary>Vrai pendant le vol (entre le tir et l'impact).</summary>
    public bool IsFlying => flying;
    /// <summary>Vrai quand la flèche est plantée quelque part.</summary>
    public bool IsStuck => stuck;
    /// <summary>Vrai si ce tir est une flèche bonus (tir triple).</summary>
    public bool IsBonus => bonus;

    /// <summary>La flèche part : (cette flèche, arc qui l'a tirée ou null).</summary>
    public event Action<ArrowHit, BowController> FlightStarted;

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

    // ---------- Flèche plantée : elle suit ce qu'elle a touché (cible qui bouge) sans changer d'orientation ----------

    Transform pinTo;                 // objet dans lequel la flèche est plantée
    Vector3 pinLocalPos;             // position de la flèche dans le repère de cet objet
    Quaternion pinLocalRot;          // orientation de la flèche dans le repère de cet objet
    bool pinned;
    RigidbodyInterpolation savedInterpolation;

    /// <summary>Fige la flèche sur 'to' : à chaque image elle est replacée exactement au même endroit de l'objet.</summary>
    void Pin(Transform to)
    {
        if (to == null) { Unpin(); return; }
        if (!pinned) savedInterpolation = rb.interpolation;
        // Pas d'interpolation sur une flèche figée : sinon le Rigidbody réécrit sa propre position
        // et la flèche "glisse" ou tourne quand la cible bouge
        rb.interpolation = RigidbodyInterpolation.None;
        pinTo = to;
        pinLocalPos = to.InverseTransformPoint(transform.position);
        pinLocalRot = Quaternion.Inverse(to.rotation) * transform.rotation;
        pinned = true;
        arrow.pinned = true;
    }

    void Unpin()
    {
        if (pinned && rb != null) rb.interpolation = savedInterpolation;
        pinned = false;
        pinTo = null;
        if (arrow != null) arrow.pinned = false;
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

        // Plantée dans une cible qui bouge : même position et même sens par rapport à la cible
        if (pinned)
        {
            if (!stuck || grab.isSelected || pinTo == null) { Unpin(); return; }
            transform.SetPositionAndRotation(pinTo.TransformPoint(pinLocalPos), pinTo.rotation * pinLocalRot);
        }
    }

    void FixedUpdate()
    {
        if (!flying) return;

        if (rb.isKinematic)
        {
            // Figée en plein vol par un autre script (ex. collision dans ArrowController) sans qu'on ait vu l'impact :
            // on termine le tir pour ne pas bloquer la manche.
            if (wasMoving && !grab.isSelected) ResolveFrozen();
            return;
        }
        wasMoving = true;

        if (!rb.useGravity && !gravityWarned && (arrow.bow == null || !arrow.bow.noGravity))
        {
            gravityWarned = true;
            Debug.LogWarning($"[ArrowHit] La gravité de '{name}' est coupée pendant le vol : la flèche vole tout droit. Vérifie Use Gravity sur son Rigidbody ou un autre script qui la coupe.", this);
        }

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
        lastDir = dir;

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
            if (h.collider.GetComponentInParent<ArrowHit>() != null) continue;          // une autre flèche
            if (IsPlayer(h.collider)) continue;                                          // le joueur lui-même
            if (arrow.bow != null && h.collider.transform.IsChildOf(arrow.bow.transform)) continue;    // l'arc
            if (closest == null || h.distance < closest.Value.distance) closest = h;
        }

        // Oiseau bonus sur la trajectoire, avant tout obstacle ?
        float maxDist = closest.HasValue ? closest.Value.distance : float.MaxValue;
        bool birdHit = BirdTarget.FindHit(previousTip, end, out var bird, out var birdPoint, out float birdDist) && birdDist <= maxDist;

        // Trajectoire visible dans la vue Scene pendant 5 s (jaune = vol)
        Debug.DrawLine(previousTip, end, Color.yellow, 5f);
        previousTip = tip;

        // Le raycast a touché le corps d'un oiseau (ses propres colliders) : c'est un oiseau touché
        if (!birdHit && closest.HasValue)
        {
            var hitBird = closest.Value.collider.GetComponentInParent<BirdTarget>();
            if (hitBird != null && !hitBird.IsHit) { birdHit = true; bird = hitBird; birdPoint = closest.Value.point; }
        }

        if (birdHit) HitBird(bird, birdPoint, dir);
        else if (closest.HasValue) StickInto(closest.Value, dir);
    }

    /// <summary>
    /// La flèche a été figée par un autre script (ex. ArrowController qui la plante lui-même au contact)
    /// avant que notre raycast ne voie l'impact. On cherche ce qu'elle a touché autour de la pointe.
    /// </summary>
    void ResolveFrozen()
    {
        Vector3 tip = arrow.tip.position;
        Vector3 start = tip - lastDir * 1f;
        Target target = null;
        Vector3 point = tip;

        // 1) Rayon le long de la flèche, un peu avant et un peu après la pointe
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(start, lastDir, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<ArrowHit>() != null) continue;
            if (arrow.bow != null && h.collider.transform.IsChildOf(arrow.bow.transform)) continue;
            var t = h.collider.GetComponentInParent<Target>();
            if (t == null || h.distance >= best) continue;
            best = h.distance; target = t; point = h.point;
        }

        // 2) Sinon, une cible tout près de la pointe
        if (target == null)
        {
            foreach (var c in Physics.OverlapSphere(tip, 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var t = c.GetComponentInParent<Target>();
                if (t == null) continue;
                target = t; point = c.ClosestPoint(tip);
                break;
            }
        }

        if (target != null)
        {
            transform.SetParent(target.transform, true);
            stuck = true;
            Pin(target.transform);
            Debug.Log($"[ArrowHit] Flèche plantée par ArrowController dans la cible '{target.name}' : impact compté.", target);
        }
        else Debug.Log("[ArrowHit] Flèche arrêtée par un autre script, aucune cible autour : raté.");

        EndFlight(target, point);
    }

    /// <summary>
    /// La flèche touche un oiseau : il donne ses points et s'envole, la flèche continue sa chute sans compter.
    /// </summary>
    void HitBird(BirdTarget bird, Vector3 point, Vector3 dir)
    {
        Debug.DrawLine(point - dir * 0.3f, point, Color.cyan, 10f);
        flying = false;
        if (!counted) return;
        counted = false;
        bird.OnArrowHit(point, dir, bonus);
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
        rb.useGravity = arrow.bow == null || !arrow.bow.noGravity;

        Vector3 axis = (arrow.tip.position - arrow.grabPoint.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, dir) * transform.rotation;
        transform.position += origin - arrow.grabPoint.position;

        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = dir * speed;
        StartFlight();
    }

    /// <summary>
    /// Lance la flèche depuis 'origin' (position de son encoche) dans la direction 'dir'.
    /// Utilisé par le tir triple du Bow_Monkey : les deux flèches latérales sont des flèches bonus
    /// (elles ne consomment pas de flèche de la manche, mais leurs points comptent).
    /// </summary>
    public void LaunchBonus(Vector3 origin, Vector3 dir, float speed, BowController fromBow)
    {
        if (grab.isSelected) return;
        if (flying) EndFlight(null);

        arrow.ResetForReuse();
        arrow.bow = fromBow;   // ignore l'arc dans les collisions
        transform.SetParent(null, true);

        Vector3 axis = (arrow.tip.position - arrow.grabPoint.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, dir) * transform.rotation;
        transform.position += origin - arrow.grabPoint.position;
        Physics.SyncTransforms();

        rb.isKinematic = false;
        rb.useGravity = fromBow == null || !fromBow.noGravity;
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = dir * speed;
        StartFlight(true);
    }

    /// <summary>Remet la flèche à zéro (reprise dans le carquois).</summary>
    public void PrepareForReuse()
    {
        Unpin();
        if (flying) EndFlight(null);
        flying = false;
        stuck = false;
        bonus = false;
        grabbedWhileStuck = false;
        StopAllCoroutines();
        transform.SetParent(null, true);
    }

    void StartFlight() => StartFlight(false);

    void StartFlight(bool isBonus)
    {
        Unpin();
        bonus = isBonus;
        flying = true;
        stuck = false;
        wasMoving = false;
        flightTime = 0f;
        gravityWarned = false;
        previousTip = arrow.tip.position;
        transform.SetParent(null, true);

        var sm = ScoreManager.Instance;
        counted = sm == null || (bonus ? sm.RegisterBonusShot() : sm.RegisterShot());
        if (!counted) Debug.Log("[ArrowHit] Manche terminée : ce tir ne compte pas.");

        FlightStarted?.Invoke(this, arrow.bow);
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
        Pin(target != null ? target.transform : hit.collider.transform);
        if (target == null) Debug.Log($"[ArrowHit] Flèche plantée dans '{hit.collider.name}' (pas une cible).", hit.collider);
        Debug.DrawLine(hit.point - dir * 0.3f, hit.point, target != null ? Color.green : Color.red, 10f);
        EndFlight(target, hit.point);
    }

    void EndFlight(Target target, Vector3 point = default)
    {
        flying = false;
        if (!rb.isKinematic) rb.useGravity = true;   // flèche sans gravité (Fulminaleuse) ratée : elle retombe

        if (!counted) return;
        counted = false;

        if (target != null) target.OnArrowHit(point, bonus);
        else if (ScoreManager.Instance != null) ScoreManager.Instance.RegisterMiss(bonus);
    }

    /// <summary>
    /// Filet de sécurité : si la physique fait toucher la flèche avant que le raycast ne voie l'impact,
    /// on compte l'impact au point de contact.
    /// </summary>
    void OnCollisionEnter(Collision collision)
    {
        if (!flying || grab.isSelected) return;

        // Choc physique contre un oiseau : il s'envole et donne ses points, la flèche continue de tomber
        var birdTouched = collision.collider.GetComponentInParent<BirdTarget>();
        if (birdTouched != null && !birdTouched.IsHit)
        {
            Vector3 p = collision.contactCount > 0 ? collision.GetContact(0).point : arrow.tip.position;
            HitBird(birdTouched, p, lastDir);
            return;
        }
        if (collision.transform.IsChildOf(transform)) return;
        if (collision.collider.GetComponentInParent<ArrowHit>() != null) return;   // une autre flèche
        if (IsPlayer(collision.collider)) return;                                  // le joueur lui-même
        if (arrow.bow != null && collision.transform.IsChildOf(arrow.bow.transform)) return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : arrow.tip.position;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        var target = collision.collider.GetComponentInParent<Target>();
        if (target != null) transform.SetParent(target.transform, true);

        stuck = true;
        Pin(target != null ? target.transform : collision.collider.transform);
        if (target == null) Debug.Log($"[ArrowHit] Flèche arrêtée par '{collision.collider.name}' (pas une cible).", collision.collider);
        EndFlight(target, point);
    }

    /// <summary>Collider du joueur (XR Origin : corps, mains) : la flèche le traverse.</summary>
    static bool IsPlayer(Collider c) => c.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null;

    /// <summary>La cible où la flèche est plantée disparaît : la flèche se détache et tombe.</summary>
    public void Unstick()
    {
        if (grab != null && grab.isSelected) return;
        Unpin();
        stuck = false;
        flying = false;
        transform.SetParent(null, true);
        rb.isKinematic = false;
        rb.useGravity = true;
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        // Le joueur reprend la flèche (plantée ou en vol) : elle n'est plus en vol.
        grabbedWhileStuck = stuck || rb.isKinematic;
        if (flying) EndFlight(null);   // rattrapée en plein vol : compte comme ratée
        Unpin();
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
