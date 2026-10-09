using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ArrowController : MonoBehaviour
{
    public Transform grabPoint;   // arrière de la flèche (encoche)
    public Transform tip;         // pointe

    [HideInInspector] public BowController bow;   // trouvé automatiquement au moment du nock

    Rigidbody rb;
    XRGrabInteractable grab;
    bool nocked;
    bool flying;    // true entre le tir et le premier choc
    bool stuck;     // true quand la flèche est plantée
    float draw;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        grab.attachTransform = grabPoint;
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;   // évite l'avertissement "kinematic Rigidbody"
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    // Flèche reprise en main (même plantée)
    void OnGrab(SelectEnterEventArgs args)
    {
        flying = false;
        stuck = false;
    }

    void Update()
    {
        if (!grab.isSelected) return;

        if (!nocked)
        {
            TryNock();
            return;
        }

        Vector3 rest = bow.arrowNock.position;
        Vector3 dir  = bow.arrowNock.forward;
        Vector3 hand = grab.firstInteractorSelecting.GetAttachTransform(grab).position;
        draw = Mathf.Clamp(Vector3.Dot(rest - hand, dir), 0f, bow.maxDraw);

        // oriente la flèche le long de l'axe de tir
        Vector3 axis = (tip.position - grabPoint.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, dir) * transform.rotation;

        // place l'arrière de la flèche au point de tension
        transform.position += (rest - dir * draw) - grabPoint.position;
    }

    // Cherche un arc libre dont l'encoche est assez proche
    void TryNock()
    {
        foreach (var b in BowController.All)
        {
            if (b.nockedArrow != null && b.nockedArrow != this) continue;
            if (Vector3.Distance(grabPoint.position, b.arrowNock.position) >= b.nockDistance) continue;

            bow = b;
            nocked = true;
            bow.nockedArrow = this;
            grab.trackPosition = false;
            grab.trackRotation = false;
            return;
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (!nocked) return;
        nocked = false;
        bow.nockedArrow = null;

        grab.trackPosition = true;
        grab.trackRotation = true;

        float t = draw / bow.maxDraw;
        float speed = Mathf.Lerp(bow.minSpeed, bow.maxSpeed, t);
        StartCoroutine(Fire(bow.arrowNock.forward, speed));
    }

    IEnumerator Fire(Vector3 dir, float speed)
    {
        yield return new WaitForFixedUpdate();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = dir * speed;
        flying = true;
    }

    void FixedUpdate()
    {
        // Seulement en vol, et assez vite
        if (!flying || rb.linearVelocity.sqrMagnitude < 1f) return;

        // La flèche suit progressivement sa trajectoire
        Vector3 axis = (tip.position - grabPoint.position).normalized;
        Vector3 velocityDirection = rb.linearVelocity.normalized;

        Quaternion turn = Quaternion.FromToRotation(axis, velocityDirection);

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                turn * rb.rotation,
                0.05f
            )
        );
    }

    // Premier choc en vol : la flèche se plante, sauf si elle touche l'arc
    void OnCollisionEnter(Collision collision)
    {
        if (!flying) return;

        // Ignore les collisions avec l'arc
        if (bow != null && collision.transform.IsChildOf(bow.transform)) return;

        // La flèche se plante
        flying = false;
        stuck = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    void LateUpdate()
    {
        // Flèche lâchée sans être tirée : elle redevient physique et tombe
        if (!grab.isSelected && rb.isKinematic && !stuck)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
}