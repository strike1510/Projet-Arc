using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Des oiseaux arrivent en volant, se posent près des cibles, restent quelques secondes, puis repartent.
/// On peut leur tirer dessus à tout moment : ils ont BirdTarget (points + texte + ils s'enfuient).
/// À mettre sur un objet vide "OiseauxEnVol" dans le niveau, avec un oiseau du pack "living birds" (ex. lb_crow)
/// dans "Bird Prefab" (n'importe quel modèle d'oiseau marche).
///
/// Les oiseaux sont de VRAIS objets de la scène, rangés (désactivés) sous l'enfant "Oiseaux en réserve".
/// "Projet Arc > Configurer le projet" les crée à partir de "Bird Prefab". Rien n'est créé pendant le jeu.
/// </summary>
public class FlyingBirdSpawner : MonoBehaviour
{
    [Header("Oiseau")]
    [Tooltip("Prefab de l'oiseau (ex. lb_crow, lb_robin… du pack living birds).")]
    public GameObject birdPrefab;

    [Tooltip("Taille de l'oiseau (1 = taille du prefab).")]
    public float birdScale = 1.5f;

    [Tooltip("Rotation ajoutée si l'oiseau vole de côté ou à reculons (90, 180, -90).")]
    public float yawOffset = 0f;

    [Tooltip("Points gagnés en touchant un oiseau.")]
    public int points = 100;

    [Tooltip("Zone touchable autour de l'oiseau (m). Plus grand = plus facile.")]
    public float hitRadius = 0.6f;

    [Header("Arrivées")]
    [Tooltip("Temps entre deux oiseaux (s), au hasard entre ces deux valeurs.")]
    public Vector2 interval = new Vector2(4f, 8f);

    [Tooltip("Nombre maximum d'oiseaux en même temps.")]
    public int maxAlive = 2;

    [Tooltip("Délai avant le premier oiseau (s).")]
    public float firstDelay = 5f;

    [Tooltip("Vitesse de vol (m/s).")]
    public Vector2 speed = new Vector2(4f, 6f);

    [Tooltip("D'où il arrive : distance horizontale depuis l'endroit où il se pose (m).")]
    public float approachDistance = 15f;

    [Tooltip("D'où il arrive : hauteur au-dessus de l'endroit où il se pose (m).")]
    public float approachHeight = 6f;

    [Header("Posé")]
    [Tooltip("Se pose à côté d'une cible de la scène (sinon devant le joueur).")]
    public bool landNearTargets = true;

    [Tooltip("Écart entre la cible et l'endroit où il se pose (m).")]
    public Vector2 besideTarget = new Vector2(0.8f, 1.5f);

    [Tooltip("S'il n'y a pas de cible : distance devant le joueur (m).")]
    public Vector2 fallbackDistance = new Vector2(6f, 10f);

    [Tooltip("Temps posé au sol (s).")]
    public Vector2 stayTime = new Vector2(3f, 6f);

    [Header("Animation")]
    [Tooltip("Nom du booléen de vol dans l'Animator de l'oiseau.")]
    public string flyingAnimatorBool = "flying";

    [Tooltip("Booléen joué de temps en temps au sol (\"peck\" = picorer dans living birds). Vide = rien.")]
    public string groundAnimatorBool = "peck";

    /// <summary>Nom de l'enfant qui contient les oiseaux en réserve.</summary>
    public const string PoolName = "Oiseaux en réserve";

    readonly List<GameObject> alive = new List<GameObject>();
    readonly List<BirdTarget> pool = new List<BirdTarget>();
    Camera cam;

    void Start()
    {
        var reserve = transform.Find(PoolName);
        if (reserve != null)
            foreach (Transform child in reserve)
                if (child.TryGetComponent(out BirdTarget b)) { pool.Add(b); child.gameObject.SetActive(false); }

        if (pool.Count == 0) { Debug.LogError($"[FlyingBirdSpawner] Aucun oiseau sous '{PoolName}' : lance Projet Arc > Configurer le projet.", this); return; }
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            // Oiseaux touchés : BirdTarget les cache à la fin de leur fuite → ils sont de nouveau libres
            alive.RemoveAll(b => b == null || !b.activeSelf);

            var sm = ScoreManager.Instance;
            if ((sm == null || !sm.IsRoundOver) && alive.Count < maxAlive) SpawnOne();
            yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
        }
    }

    // ---------- Où se poser ----------

    bool FindLandingSpot(out Vector3 spot)
    {
        spot = default;
        if (cam == null || !cam.isActiveAndEnabled) cam = TargetSpawner.FindGameViewCamera();
        if (cam == null) return false;
        Vector3 eye = cam.transform.position;

        // 1) À côté d'une cible visible
        if (landNearTargets)
        {
            var targets = new List<Target>(FindObjectsByType<Target>());
            for (int i = 0; i < targets.Count; i++) { int j = Random.Range(i, targets.Count); (targets[i], targets[j]) = (targets[j], targets[i]); }

            foreach (var t in targets)
            {
                if (t == null || !t.isActiveAndEnabled) continue;
                Vector3 side = t.transform.right * (Random.value < 0.5f ? -1f : 1f);
                Vector3 p = t.transform.position + side * Random.Range(besideTarget.x, besideTarget.y);
                if (Ground(p, out spot) && Clear(spot) && Visible(eye, spot)) return true;
            }
        }

        // 2) Sinon devant le joueur
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        for (int k = 0; k < 10; k++)
        {
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-30f, 30f), Vector3.up) * fwd;
            Vector3 p = eye + dir * Random.Range(fallbackDistance.x, fallbackDistance.y);
            if (Ground(p, out spot) && Clear(spot) && Visible(eye, spot)) return true;
        }
        return false;
    }

    static bool Ground(Vector3 p, out Vector3 ground)
    {
        ground = p;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<Target>() != null) continue;   // pas sur la cible elle-même
            if (h.collider.GetComponentInParent<ArrowHit>() != null) continue;
            if (h.point.y > ground.y || ground == p) ground = h.point;
        }
        return ground != p;
    }

    static bool Clear(Vector3 spot) =>
        !Physics.CheckSphere(spot + Vector3.up * 0.4f, 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

    static bool Visible(Vector3 eye, Vector3 spot)
    {
        Vector3 to = spot + Vector3.up * 0.2f;
        foreach (var h in Physics.RaycastAll(eye, (to - eye).normalized, Vector3.Distance(eye, to) - 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponentInParent<Target>() == null) return false;   // une cible devant, ça passe
        return true;
    }

    // ---------- Un oiseau ----------

    void SpawnOne()
    {
        if (!FindLandingSpot(out Vector3 land)) return;

        // Il arrive d'un côté, en hauteur, et repart de l'autre
        Vector3 toPlayer = Vector3.ProjectOnPlane(cam.transform.position - land, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer) * (Random.value < 0.5f ? -1f : 1f);
        Vector3 from = land + (side * 0.8f - toPlayer * 0.6f).normalized * approachDistance + Vector3.up * approachHeight;
        Vector3 leave = land + (-side * 0.8f - toPlayer * 0.6f).normalized * approachDistance + Vector3.up * approachHeight * 1.5f;

        // Un oiseau libre de la réserve (déjà réglé dans l'éditeur : scripts du pack coupés, Rigidbody cinématique)
        BirdTarget bt = null;
        foreach (var b in pool) if (b != null && !b.gameObject.activeSelf) { bt = b; break; }
        if (bt == null) return;
        var bird = bt.gameObject;

        bird.transform.position = from;
        bt.points = points;
        bt.hitRadius = hitRadius;
        bt.reaction = BirdTarget.Reaction.SEnvole;
        bt.flyingAnimatorBool = flyingAnimatorBool;

        bird.SetActive(true);
        alive.Add(bird);
        StartCoroutine(Visit(bird, bt, from, land, leave, Random.Range(speed.x, speed.y)));
    }

    IEnumerator Visit(GameObject bird, BirdTarget bt, Vector3 from, Vector3 land, Vector3 leave, float v)
    {
        var anim = bird.GetComponentInChildren<Animator>();

        // 1) Arrivée : courbe qui descend vers l'endroit où il se pose
        SetBool(anim, flyingAnimatorBool, true);
        Vector3 control = Vector3.Lerp(from, land, 0.5f) + Vector3.up * approachHeight * 0.3f;
        yield return FlyCurve(bird, bt, from, control, land, v, slowDownAtEnd: true);
        if (Gone(bird, bt)) yield break;

        // 2) Posé : il regarde vers le joueur, picore de temps en temps
        SetBool(anim, flyingAnimatorBool, false);
        Vector3 look = Vector3.ProjectOnPlane(cam.transform.position - land, Vector3.up);
        if (look.sqrMagnitude > 0.01f)
            bird.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up) * look) * Quaternion.Euler(0f, yawOffset, 0f);

        float stay = Random.Range(stayTime.x, stayTime.y);
        for (float t = 0f; t < stay; t += 0.5f)
        {
            if (Gone(bird, bt)) yield break;
            if (!string.IsNullOrEmpty(groundAnimatorBool)) SetBool(anim, groundAnimatorBool, Random.value < 0.35f);
            yield return new WaitForSeconds(0.5f);
        }
        if (Gone(bird, bt)) yield break;
        SetBool(anim, groundAnimatorBool, false);

        // 3) Départ : petit bond puis il s'envole de l'autre côté
        SetBool(anim, flyingAnimatorBool, true);
        Vector3 control2 = land + Vector3.up * 2.5f + (leave - land).normalized * 2f;
        yield return FlyCurve(bird, bt, land, control2, leave, v, slowDownAtEnd: false);
        if (Gone(bird, bt)) yield break;

        bird.SetActive(false);   // retourne dans la réserve
    }

    /// <summary>Vol le long d'une courbe (Bézier) de a à c en passant près de b. S'arrête si l'oiseau est touché.</summary>
    IEnumerator FlyCurve(GameObject bird, BirdTarget bt, Vector3 a, Vector3 b, Vector3 c, float v, bool slowDownAtEnd)
    {
        float length = Vector3.Distance(a, b) + Vector3.Distance(b, c);
        float bobSeed = Random.value * 10f;
        Vector3 prev = a;

        for (float s = 0f; s < 1f; )
        {
            if (Gone(bird, bt)) yield break;

            // Ralentit à l'approche du sol pour se poser en douceur
            float speedNow = v * (slowDownAtEnd ? Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(0.7f, 1f, s)) : Mathf.Lerp(0.5f, 1f, s * 3f));
            s = Mathf.Min(1f, s + speedNow * Time.deltaTime / Mathf.Max(0.1f, length));

            Vector3 p = Bezier(a, b, c, s);
            if (!slowDownAtEnd || s < 0.85f) p.y += Mathf.Sin(Time.time * 6f + bobSeed) * 0.08f;   // petit battement
            bird.transform.position = p;

            Vector3 flat = Vector3.ProjectOnPlane(p - prev, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f)
                bird.transform.rotation = Quaternion.Slerp(bird.transform.rotation,
                    Quaternion.LookRotation(flat) * Quaternion.Euler(0f, yawOffset, 0f), 10f * Time.deltaTime);
            prev = p;
            yield return null;
        }
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static bool Gone(GameObject bird, BirdTarget bt) => bird == null || !bird.activeInHierarchy || bt == null || bt.IsHit;

    static void SetBool(Animator anim, string name, bool value)
    {
        if (anim == null || string.IsNullOrEmpty(name) || anim.runtimeAnimatorController == null) return;
        foreach (var p in anim.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name) { anim.SetBool(name, value); return; }
    }

}
