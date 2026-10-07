using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ArrowController : MonoBehaviour
{
    public Transform grabPoint;   // arrière de la flèche (encoche)
    public Transform tip;         // pointe
    public BowController bow;

    Rigidbody rb;
    XRGrabInteractable grab;
    bool nocked;
    float draw;

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
        Vector3 dir  = bow.arrowNock.forward;

        if (!nocked)
        {
            if (Vector3.Distance(grabPoint.position, rest) < bow.nockDistance)
            {
                nocked = true;
                bow.nockedArrow = this;
                grab.trackPosition = false;
                grab.trackRotation = false;
                grab.throwOnDetach = false;
            }
            return;
        }

        Vector3 hand = grab.firstInteractorSelecting.GetAttachTransform(grab).position;
        draw = Mathf.Clamp(Vector3.Dot(rest - hand, dir), 0f, bow.maxDraw);

        // oriente la flèche le long de l'axe de tir
        Vector3 axis = (tip.position - grabPoint.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, dir) * transform.rotation;

        // place l'arrière de la flèche au point de tension
        transform.position += (rest - dir * draw) - grabPoint.position;
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
    }
}