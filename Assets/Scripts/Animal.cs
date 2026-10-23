using System.Collections;
using UnityEngine;

/// <summary>
/// Animal qui se balade autour de son point de départ. Touché par une flèche : +20, il s'enfuit en courant
/// puis disparaît, et réapparaît plus tard. Il s'enfuit aussi si le joueur s'approche trop.
/// À mettre sur la racine d'un animal posé dans la scène (ex. pack "Animals FREE" d'ithappy).
/// </summary>
public class Animal : CreatureBase
{
    [Header("Fuite")]
    [Tooltip("Vitesse de course quand il fuit (m/s).")]
    public float runSpeed = 5f;

    [Tooltip("Durée de la fuite après avoir été touché (s).")]
    public float fleeTime = 3f;

    [Tooltip("S'éloigne si le joueur s'approche à moins de cette distance (m). 0 = jamais.")]
    public float shyDistance = 4f;

    [Header("Marche naturelle")]
    [Tooltip("Marche en continu en tournant doucement, avec quelques pauses. Décoché : va de point en point.")]
    public bool naturalWalk = true;
    [Tooltip("Vitesse à laquelle il change de direction (degrés/s). Plus petit = trajets plus droits.")]
    public float turnSpeed = 40f;
    [Tooltip("Durée d'une balade avant de peut-être faire une pause (s).")]
    public Vector2 walkDuration = new Vector2(8f, 20f);
    [Tooltip("Chance de faire une pause à la fin d'une balade (0 = jamais, 1 = toujours).")]
    [Range(0f, 1f)] public float pauseChance = 0.4f;

    [Header("Vie selon la taille")]
    [Tooltip("Plus l'animal est gros, plus il faut de flèches (et plus il rapporte). Remplace 'Health' et 'Points'.")]
    public bool healthFromSize = true;
    [Tooltip("Taille (m) qui vaut 1 point de vie : poule ≈ 1 PV, chien ≈ 2, cerf ≈ 3, cheval ≈ 4.")]
    public float metersPerHealth = 0.6f;
    [Tooltip("Points gagnés par point de vie de l'animal.")]
    public int pointsPerHealth = 10;

    [Header("États de l'Animator")]
    public string runState = "Run";

    bool walking;
    float timer;
    Vector3 target;

    void Reset()
    {
        points = 20;
        walkSpeed = 1.2f;
        wanderRadius = 8f;
    }

    protected override void Awake()
    {
        base.Awake();
        if (healthFromSize)
        {
            health = Mathf.Clamp(Mathf.RoundToInt(ModelSize() / Mathf.Max(0.1f, metersPerHealth)), 1, 8);
            hp = health;
            points = health * pointsPerHealth;
        }
    }

    protected override void Start()
    {
        base.Start();
        timer = Random.Range(idleTime.x, idleTime.y);
    }

    void Update()
    {
        if (dead) return;

        if (Time.time < panicUntil)
        {
            Play(runState.Length > 0 ? runState : walkState);
            MoveTowards(transform.position + panicDir * 3f, runSpeed);
            return;
        }

        // Le joueur s'approche : il s'écarte en trottinant
        if (shyDistance > 0f)
        {
            Vector3 away = transform.position - PlayerHead();
            away.y = 0f;
            if (away.magnitude < shyDistance)
            {
                Play(runState.Length > 0 ? runState : walkState);
                MoveTowards(transform.position + away.normalized * 2f, runSpeed * 0.6f);
                return;
            }
        }

        if (naturalWalk) { NaturalWalk(); return; }

        if (!walking)
        {
            Play(idleState);
            timer -= Time.deltaTime;
            if (timer <= 0f) { target = RandomPointAroundHome(); walking = true; }
        }
        else
        {
            Play(walkState);
            if (MoveTowards(target, walkSpeed)) { walking = false; timer = Random.Range(idleTime.x, idleTime.y); }
        }
    }

    // ---------- Marche naturelle ----------

    float heading;          // direction actuelle (degrés)
    float walkLeft;         // temps de marche avant la prochaine pause
    float pauseLeft;        // temps de pause restant
    float noiseSeed;
    float avoidTurn;        // virage en cours pour contourner un obstacle

    void NaturalWalk()
    {
        if (noiseSeed == 0f)
        {
            noiseSeed = Random.Range(1f, 1000f);
            heading = transform.eulerAngles.y - modelYawOffset;
            walkLeft = Random.Range(walkDuration.x, walkDuration.y);
        }

        // Petite pause de temps en temps (brouter, regarder autour)
        if (pauseLeft > 0f)
        {
            Play(idleState);
            pauseLeft -= Time.deltaTime;
            if (pauseLeft <= 0f) walkLeft = Random.Range(walkDuration.x, walkDuration.y);
            return;
        }
        walkLeft -= Time.deltaTime;
        if (walkLeft <= 0f)
        {
            if (Random.value < pauseChance) { pauseLeft = Random.Range(idleTime.x, idleTime.y); return; }
            walkLeft = Random.Range(walkDuration.x, walkDuration.y);
        }

        // La direction dérive doucement (comme un animal qui flâne)
        float t = Time.time * 0.25f;
        heading += (Mathf.PerlinNoise(noiseSeed, t) - 0.5f) * 2f * turnSpeed * Time.deltaTime;

        // Trop loin de sa zone : il revient progressivement vers le centre
        Vector3 toHome = home - transform.position;
        toHome.y = 0f;
        if (toHome.magnitude > wanderRadius * 0.8f)
        {
            float homeAngle = Mathf.Atan2(toHome.x, toHome.z) * Mathf.Rad2Deg;
            float pull = Mathf.InverseLerp(wanderRadius * 0.8f, wanderRadius * 1.2f, toHome.magnitude);
            heading = Mathf.MoveTowardsAngle(heading, homeAngle, Mathf.Lerp(20f, 120f, pull) * Time.deltaTime);
        }

        // Contourne les obstacles en tournant au lieu de s'arrêter
        if (avoidTurn != 0f)
        {
            float step = Mathf.Sign(avoidTurn) * Mathf.Min(Mathf.Abs(avoidTurn), 180f * Time.deltaTime);
            heading += step;
            avoidTurn -= step;
        }

        Vector3 dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
        float speed = walkSpeed * Mathf.Lerp(0.8f, 1.2f, Mathf.PerlinNoise(noiseSeed + 30f, t));
        Play(walkState);
        if (MoveTowards(transform.position + dir * 2f, speed) && avoidTurn == 0f)
            avoidTurn = Random.Range(90f, 160f) * (Random.value < 0.5f ? -1f : 1f);   // bloqué : demi-tour partiel
    }

    protected override bool IsRunState(string state) => state == runState;

    float panicUntil;
    Vector3 panicDir;

    // Touché mais pas encore abattu : il affiche ses PV restants et détale un instant
    protected override void OnHurt(Vector3 direction)
    {
        HitPopup.Show(transform.position + Vector3.up * (ModelSize() * 0.8f + 0.3f), $"{hp}/{health}", new Color(1f, 0.55f, 0.4f), 0.5f, 1f);
        panicDir = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        panicUntil = Time.time + 1.5f;
    }

    protected override string PopupText() => $"+{points}";
    protected override Color PopupColor() => new Color(0.6f, 1f, 0.5f);

    // Touché : il ne meurt pas, il s'enfuit et disparaît
    protected override IEnumerator DieRoutine(Vector3 direction)
    {
        Vector3 flee = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (flee.sqrMagnitude < 0.01f) flee = transform.forward;
        Play(runState.Length > 0 ? runState : walkState, 0.05f);

        for (float t = 0f; t < fleeTime; t += Time.deltaTime)
        {
            MoveTowards(transform.position + flee * 3f, runSpeed);
            yield return null;
        }
        yield return HideAndRespawn();
    }

    protected override void OnRespawn() { walking = false; timer = Random.Range(idleTime.x, idleTime.y); noiseSeed = 0f; pauseLeft = 0f; avoidTurn = 0f; }
}
