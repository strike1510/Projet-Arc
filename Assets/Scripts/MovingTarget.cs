using UnityEngine;

/// <summary>
/// Fait bouger une cible. À mettre sur la racine d'une cible, à côté de Target.
/// Les flèches plantées dedans bougent avec elle.
///
/// Mouvement "Auto" (par défaut) : une cible au sol va de gauche à droite, une cible qui vole monte et descend.
/// "Aléatoire" coché : la cible change de vitesse, de sens et d'écart au hasard (imprévisible).
/// </summary>
[RequireComponent(typeof(Target))]
public class MovingTarget : MonoBehaviour
{
    public enum Motion { GaucheDroite, HautBas, Cercle, Auto }

    [Tooltip("Type de mouvement. Auto : au sol → gauche/droite, en l'air → haut/bas.")]
    public Motion motion = Motion.Auto;

    [Tooltip("Distance maximum parcourue de part et d'autre du point de départ (m).")]
    public float amplitude = 2f;

    [Tooltip("Vitesse (0.25 = lent, 1 = rapide).")]
    public float speed = 0.4f;

    [Tooltip("Décalage de départ (0 à 1) : évite que toutes les cibles bougent en même temps.")]
    [Range(0f, 1f)] public float phase = 0f;

    [Tooltip("Mouvement imprévisible : vitesse, sens et écart changent au hasard.")]
    public bool aleatoire = true;

    [Tooltip("Mode Auto : au-dessus de cette hauteur par rapport au sol, la cible est considérée comme volante (m).")]
    public float flyingHeight = 0.8f;

    Rigidbody rb;
    Vector3 startPosition;
    Motion resolved;
    float seed;

    /// <summary>Le mouvement réellement utilisé (Auto remplacé par GaucheDroite ou HautBas).</summary>
    public Motion ResolvedMotion => resolved;

    void Awake()
    {
        startPosition = transform.position;
        seed = Random.Range(0f, 1000f) + phase * 100f;

        // Rigidbody cinématique : les colliders qui bougent restent bien détectés par les flèches
        // (posé dans l'éditeur par "Projet Arc > Configurer le projet")
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning($"[MovingTarget] '{name}' n'a pas de Rigidbody : lance Projet Arc > Configurer le projet.", this);
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start() => Restart();

    /// <summary>Repart de la position actuelle (cible réutilisée par le TargetSpawner).</summary>
    public void Restart()
    {
        startPosition = transform.position;
        clock = 0f;
        if (rb != null) rb.position = transform.position;

        // Start et pas Awake : le Spawner peut régler "motion" juste après l'apparition
        resolved = motion == Motion.Auto ? (IsFlying() ? Motion.HautBas : Motion.GaucheDroite) : motion;

        // Une cible qui monte et descend ne doit pas rentrer dans le sol : on la remonte si besoin
        if (resolved == Motion.HautBas || resolved == Motion.Cercle)
        {
            float clearance = HeightAboveGround() - amplitude;
            if (clearance < 0.3f) startPosition.y += 0.3f - clearance;
        }
    }

    /// <summary>Distance entre le bas de la cible et le sol en dessous (100 si rien).</summary>
    float HeightAboveGround()
    {
        float bottom = transform.position.y;
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger) bottom = Mathf.Min(bottom, c.bounds.min.y);

        foreach (var hit in Physics.RaycastAll(new Vector3(transform.position.x, bottom + 0.05f, transform.position.z), Vector3.down, 100f,
                                              Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            return hit.distance;
        }
        return 100f;
    }

    /// <summary>Vrai si la cible est en l'air (pas de sol juste en dessous de son point le plus bas).</summary>
    public bool IsFlying() => HeightAboveGround() > flyingHeight;

    float clock;   // temps de mouvement (s'arrête quand la manche est finie)

    void FixedUpdate()
    {
        // Plus de flèches / manche finie : la cible s'arrête là où elle est
        var sm = ScoreManager.Instance;
        if (sm != null && sm.IsRoundOver) return;

        clock += Time.fixedDeltaTime;
        float t = clock;
        float a, b;   // deux valeurs entre -1 et 1

        if (aleatoire)
        {
            // Bruit de Perlin : va et vient au hasard, avec des accélérations, des pauses et des demi-tours
            a = Mathf.PerlinNoise(seed, t * speed) * 2f - 1f;
            b = Mathf.PerlinNoise(seed + 50f, t * speed) * 2f - 1f;
            a = Mathf.Clamp(a * 1.6f, -1f, 1f);   // le Perlin reste souvent près du centre : on étire
            b = Mathf.Clamp(b * 1.6f, -1f, 1f);
        }
        else
        {
            float angle = (t * speed + phase) * Mathf.PI * 2f;
            a = Mathf.Sin(angle);
            b = Mathf.Cos(angle);
        }

        Vector3 right = transform.right, up = Vector3.up;
        Vector3 offset = resolved switch
        {
            Motion.HautBas => up * (a * amplitude),
            Motion.Cercle => right * (b * amplitude) + up * (a * amplitude),
            _ => right * (a * amplitude),
        };

        rb.MovePosition(startPosition + offset);
    }

    // Montre la zone de mouvement dans la vue Scene quand la cible est sélectionnée
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? startPosition : transform.position;
        Motion m = Application.isPlaying ? resolved : (motion == Motion.Auto ? (IsFlying() ? Motion.HautBas : Motion.GaucheDroite) : motion);
        Gizmos.color = Color.yellow;
        switch (m)
        {
            case Motion.HautBas:
                Gizmos.DrawLine(center - Vector3.up * amplitude, center + Vector3.up * amplitude);
                break;
            case Motion.Cercle:
                Vector3 prev = center + transform.right * amplitude;
                for (int i = 1; i <= 32; i++)
                {
                    float ang = i / 32f * Mathf.PI * 2f;
                    Vector3 p = center + transform.right * (Mathf.Cos(ang) * amplitude) + Vector3.up * (Mathf.Sin(ang) * amplitude);
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
