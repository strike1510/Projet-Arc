using System.Collections;
using UnityEngine;

/// <summary>
/// Flèche bonus du tir triple : vole toute seule (trajectoire calculée ici, sans physique),
/// se plante dans ce qu'elle touche, donne des points bonus sur une cible, puis disparaît.
/// Elle ne compte pas comme une flèche tirée.
/// </summary>
public class BonusArrow : MonoBehaviour
{
    public float penetration = 0.05f;

    [Tooltip("Temps avant que la flèche plantée disparaisse (s).")]
    public float lifetimeAfterStop = 8f;

    public bool Stopped { get; private set; }

    Transform tip, back, ignoreRoot;
    Vector3 velocity, prevTip;
    float pointsFactor, age;

    public void Launch(Transform arrowTip, Transform arrowBack, Vector3 startVelocity, float factor, Transform ignore)
    {
        tip = arrowTip;
        back = arrowBack;
        velocity = startVelocity;
        pointsFactor = factor;
        ignoreRoot = ignore;
        prevTip = tip.position;
    }

    void Update()
    {
        if (Stopped || tip == null) return;
        float dt = Time.deltaTime;
        age += dt;

        if (age > 8f || transform.position.y < -50f)
        {
            Destroy(gameObject);
            return;
        }

        velocity += Physics.gravity * dt;

        // La pointe suit la trajectoire
        Vector3 axis = (tip.position - back.position).normalized;
        transform.rotation = Quaternion.FromToRotation(axis, velocity.normalized) * transform.rotation;
        transform.position += velocity * dt;

        Vector3 segment = tip.position - prevTip;
        if (segment.sqrMagnitude > 1e-8f && FindHit(prevTip, segment, out var hit))
        {
            Stick(hit, segment.normalized);
            return;
        }
        prevTip = tip.position;
    }

    bool FindHit(Vector3 from, Vector3 segment, out RaycastHit best)
    {
        best = default;
        bool found = false;
        foreach (var h in Physics.RaycastAll(from, segment.normalized, segment.magnitude,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            var t = h.collider.transform;
            if (t.IsChildOf(transform)) continue;
            if (ignoreRoot != null && t.IsChildOf(ignoreRoot)) continue;                 // l'arc
            if (h.collider.GetComponentInParent<ArrowController>() != null) continue;    // les autres flèches
            if (h.collider.GetComponentInParent<BirdTarget>() != null) continue;
            if (h.collider is CharacterController) continue;                             // le joueur
            if (found && h.distance >= best.distance) continue;
            best = h;
            found = true;
        }
        return found;
    }

    void Stick(RaycastHit hit, Vector3 dir)
    {
        Stopped = true;
        transform.position += (hit.point + dir * penetration) - tip.position;

        var target = hit.collider.GetComponentInParent<Target>();
        if (target != null)
        {
            transform.SetParent(target.transform, true);   // suit la cible si elle bouge
            target.OnBonusArrowHit(hit.point, pointsFactor);
        }
        StartCoroutine(Disappear());
    }

    IEnumerator Disappear()
    {
        yield return new WaitForSeconds(lifetimeAfterStop);
        Vector3 start = transform.localScale;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            transform.localScale = start * (1f - t / 0.4f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
