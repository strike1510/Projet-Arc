using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ArrowController : MonoBehaviour
{
    public Transform grabPoint;   // arrière de la flèche
    public Transform tip;         // pointe de la flèche
    public BowController bow;

    Rigidbody rb;
    XRGrabInteractable grab;
    bool nocked;
    float draw;

    bool scored;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        grab.attachTransform = grabPoint;
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.selectExited.AddListener(OnRelease);
    }

    void Update()
    {
        if (!grab.isSelected) return;

        Vector3 rest = bow.arrowNock.position;
        Vector3 dir = bow.arrowNock.forward;

        if (!nocked)
        {
            if (Vector3.Distance(grabPoint.position, rest) < bow.nockDistance)
            {
                nocked = true;
<<<<<<< Updated upstream
=======
                scored = false;
                bow.nockedArrow = this;
>>>>>>> Stashed changes
                grab.trackPosition = false;
                grab.trackRotation = false;
                grab.throwOnDetach = false;
            }
            return;
        }

        Vector3 hand = grab.firstInteractorSelecting.GetAttachTransform(grab).position;
        draw = Mathf.Clamp(Vector3.Dot(rest - hand, dir), 0f, bow.maxDraw);

        Vector3 axis = (tip.position - grabPoint.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, dir) * transform.rotation;
        transform.position += (rest - dir * draw) - grabPoint.position;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        if (!nocked) return;
        nocked = false;
        grab.trackPosition = true;
        grab.trackRotation = true;
        grab.throwOnDetach = true;
        float t = draw / bow.maxDraw;
        StartCoroutine(Fire(bow.arrowNock.forward, Mathf.Lerp(bow.minSpeed, bow.maxSpeed, t)));
    }

    IEnumerator Fire(Vector3 dir, float speed)
    {
        yield return new WaitForFixedUpdate();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = dir * speed;
    }
<<<<<<< Updated upstream
=======

    void FixedUpdate()
    {
        // Ne fait rien si la flèche est immobile ou encore tenue
        if (rb.isKinematic || rb.linearVelocity.sqrMagnitude < 1f) return;

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

    // Premier choc en vol : la flèche se plante
    // sauf si elle touche l'arc
    void OnCollisionEnter(Collision collision)
    {
        // Si la flèche est encore kinematic, elle n'est pas en vol
        if (rb.isKinematic) return;

        // Ignore les collisions avec l'arc
        if (collision.transform.IsChildOf(bow.transform)) return;

        // Score si on touche un anneau de cible
        if (!scored)
        {
            TargetRing ring = collision.collider.GetComponent<TargetRing>();
            if (ring != null)
            {
                scored = true;
                ScoreManager.Instance.AddPoints(ring.points);
            }
        }

        // La flèche se plante
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    void LateUpdate()
    {
        if (!grab.isSelected && rb.isKinematic)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
>>>>>>> Stashed changes
}