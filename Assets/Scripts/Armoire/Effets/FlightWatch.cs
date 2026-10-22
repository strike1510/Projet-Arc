using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Suit le vol d'une flèche pour les effets : direction, distance parcourue,
/// et détecte la fin du vol (plantée, posée par terre, ou rattrapée à la main).
/// </summary>
public class FlightWatch
{
    readonly Transform tip;
    readonly Rigidbody rb;
    readonly BonusArrow bonus;
    readonly XRGrabInteractable grab;
    Vector3 last;
    float age;
    int stillFrames;

    public Vector3 Direction { get; private set; }
    public float Travelled { get; private set; }

    public FlightWatch(GameObject arrow, Transform arrowTip, Vector3 startDirection)
    {
        tip = arrowTip;
        arrow.TryGetComponent(out rb);
        arrow.TryGetComponent(out bonus);
        arrow.TryGetComponent(out grab);
        last = tip.position;
        Direction = startDirection.sqrMagnitude > 1e-6f ? startDirection.normalized : Vector3.forward;
    }

    /// <summary>Renvoie true quand le vol est fini. grabbed = la flèche a été reprise en main.</summary>
    public bool Tick(float dt, out bool grabbed)
    {
        grabbed = grab != null && grab.isSelected;
        if (tip == null) return true;
        if (grabbed) return true;

        age += dt;
        Vector3 moved = tip.position - last;
        last = tip.position;
        if (moved.sqrMagnitude > 1e-6f) Direction = moved.normalized;
        Travelled += moved.magnitude;

        if (age < 0.12f) return false;

        // Flèche bonus : elle sait elle-même quand elle est plantée
        if (bonus != null) return bonus.Stopped;

        // Flèche normale : ArrowHit la fige (kinematic) quand elle se plante
        if (rb != null && rb.isKinematic) return true;

        // Sinon elle a fini par s'arrêter (tombée par terre)
        stillFrames = moved.magnitude < 0.002f ? stillFrames + 1 : 0;
        return stillFrames >= 3 || age > 10f;
    }
}
