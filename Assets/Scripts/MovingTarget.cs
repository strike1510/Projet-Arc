using UnityEngine;

/// <summary>
/// Fait bouger une cible en douceur (va-et-vient). À mettre sur la racine d'une cible, à côté de Target.
/// Les flèches plantées dedans bougent avec elle.
/// </summary>
[RequireComponent(typeof(Target))]
public class MovingTarget : MonoBehaviour
{
    public enum Motion { GaucheDroite, HautBas, Cercle }

    [Tooltip("Type de mouvement.")]
    public Motion motion = Motion.GaucheDroite;

    [Tooltip("Distance parcourue de part et d'autre du point de départ (m).")]
    public float amplitude = 2f;

    [Tooltip("Allers-retours par seconde (0.25 = lent, 1 = rapide).")]
    public float speed = 0.4f;

    [Tooltip("Décalage de départ (0 à 1) : évite que toutes les cibles bougent en même temps.")]
    [Range(0f, 1f)] public float phase = 0f;

    Rigidbody rb;
    Vector3 startPosition;

    void Awake()
    {
        startPosition = transform.position;

        // Rigidbody cinématique : les colliders qui bougent restent bien détectés par les flèches
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        float angle = (Time.time * speed + phase) * Mathf.PI * 2f;
        Vector3 right = transform.right, up = Vector3.up;

        Vector3 offset = motion switch
        {
            Motion.HautBas => up * (Mathf.Sin(angle) * amplitude),
            Motion.Cercle => right * (Mathf.Cos(angle) * amplitude) + up * (Mathf.Sin(angle) * amplitude),
            _ => right * (Mathf.Sin(angle) * amplitude),
        };

        rb.MovePosition(startPosition + offset);
    }

    // Montre la trajectoire dans la vue Scene quand la cible est sélectionnée
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.yellow;
        switch (motion)
        {
            case Motion.HautBas:
                Gizmos.DrawLine(center - Vector3.up * amplitude, center + Vector3.up * amplitude);
                break;
            case Motion.Cercle:
                Vector3 prev = center + transform.right * amplitude;
                for (int i = 1; i <= 32; i++)
                {
                    float a = i / 32f * Mathf.PI * 2f;
                    Vector3 p = center + transform.right * (Mathf.Cos(a) * amplitude) + Vector3.up * (Mathf.Sin(a) * amplitude);
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }
                break;
            default:
                Gizmos.DrawLine(center - transform.right * amplitude, center + transform.right * amplitude);
                break;
        }
    }
}
