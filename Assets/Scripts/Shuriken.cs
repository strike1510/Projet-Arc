using UnityEngine;

/// <summary>
/// Projectile lancé par un ninja : vole tout droit en tournant, retire des points de vie au joueur s'il le touche,
/// se plante dans ce qu'il rencontre sinon. Créé par Ninja (pas besoin de le poser dans la scène).
/// </summary>
public class Shuriken : MonoBehaviour
{
    public float speed = 10f;
    public int damage = 1;
    public float spinSpeed = 1080f;   // degrés par seconde
    public float lifetime = 6f;

    Vector3 direction;
    Transform thrower;
    bool stopped;
    float age;

    /// <summary>Lance un shuriken. prefab vide = petit shuriken gris créé à la volée.</summary>
    public static Shuriken Launch(GameObject prefab, Vector3 from, Vector3 dir, float speed, int damage, Transform thrower)
    {
        GameObject go = prefab != null ? Instantiate(prefab, from, Quaternion.LookRotation(dir)) : MakeDefault(from, dir);
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;   // on gère les chocs nous-mêmes
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;

        var s = go.GetComponent<Shuriken>();
        if (s == null) s = go.AddComponent<Shuriken>();
        s.direction = dir.normalized;
        s.speed = speed;
        s.damage = damage;
        s.thrower = thrower;
        return s;
    }

    static GameObject MakeDefault(Vector3 from, Vector3 dir)
    {
        var root = new GameObject("Shuriken");
        root.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
        var blades = new GameObject("Lames");
        blades.transform.SetParent(root.transform, false);
        for (int i = 0; i < 2; i++)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(blades.transform, false);
            blade.transform.localScale = new Vector3(0.3f, 0.02f, 0.06f);
            blade.transform.localRotation = Quaternion.Euler(0f, i * 90f + 45f, 0f);
            blade.GetComponent<Renderer>().material.color = new Color(0.25f, 0.25f, 0.28f);
        }
        return root;
    }

    void Update()
    {
        if (stopped) return;
        age += Time.deltaTime;
        if (age > lifetime) { Destroy(gameObject); return; }

        Vector3 step = direction * speed * Time.deltaTime;

        // Le joueur (zone autour de la tête et du corps) ?
        var player = PlayerHealth.Instance;
        if (player != null && player.IsHitBySegment(transform.position, transform.position + step, 0.25f))
        {
            player.TakeDamage(damage, transform.position);
            Destroy(gameObject);
            return;
        }

        // Un obstacle (arbre, mur, sol) : il s'y plante et disparaît
        foreach (var h in Physics.RaycastAll(transform.position, direction, step.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (thrower != null && h.collider.transform.IsChildOf(thrower)) continue;
            if (player != null && h.collider.transform.IsChildOf(player.transform)) continue;
            transform.position = h.point;
            stopped = true;
            Destroy(gameObject, 3f);
            return;
        }

        transform.position += step;
        // Rotation à plat autour de l'axe vertical du shuriken (il "tourne" en vol)
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
    }
}
