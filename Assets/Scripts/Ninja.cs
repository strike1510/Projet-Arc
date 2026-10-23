using UnityEngine;

/// <summary>
/// Ninja ennemi : se balade autour de son point de départ, repère le joueur, s'arrête, le regarde
/// et lui lance des shurikens. Une flèche l'abat (+50), il réapparaît plus tard.
/// À mettre sur la racine d'un ninja posé dans la scène (modèle animé, ex. Mixamo).
/// </summary>
public class Ninja : CreatureBase
{
    [Header("Attaque")]
    [Tooltip("Distance à laquelle il repère le joueur (m).")]
    public float detectRange = 20f;

    [Tooltip("Temps entre deux lancers (s).")]
    public float fireInterval = 2.5f;

    [Tooltip("Délai entre le début de l'animation de lancer et le départ du shuriken (s).")]
    public float throwDelay = 0.4f;

    [Tooltip("Prefab du projectile (un modèle de shuriken). Vide = shuriken simple créé par le script.")]
    public GameObject projectilePrefab;

    [Tooltip("Main ou point d'où part le shuriken. Vide = devant la poitrine.")]
    public Transform throwPoint;

    [Tooltip("Vitesse du shuriken (m/s). Plus lent = plus facile à esquiver.")]
    public float projectileSpeed = 10f;

    [Tooltip("Imprécision (m à 10 m de distance). 0 = toujours en plein visage.")]
    public float inaccuracy = 0.5f;

    [Tooltip("Dégâts d'un shuriken (points de vie du joueur).")]
    public int damage = 1;

    [Header("États de l'Animator")]
    public string throwState = "Throw";

    enum Mode { Idle, Walk, Attack }
    Mode mode = Mode.Idle;
    float modeTimer;
    Vector3 walkTarget;
    float nextThrow;
    float throwAt = -1f;

    protected override void Start()
    {
        base.Start();
        modeTimer = Random.Range(idleTime.x, idleTime.y);
        nextThrow = Time.time + Random.Range(1f, fireInterval);
    }

    void Update()
    {
        if (dead) return;

        Vector3 head = PlayerHead();
        bool seesPlayer = Vector3.Distance(transform.position, head) <= detectRange && CanSee(head);

        if (seesPlayer) Attack(head);
        else Wander();
    }

    void Wander()
    {
        throwAt = -1f;
        if (mode == Mode.Attack) { mode = Mode.Idle; modeTimer = 1f; }

        if (mode == Mode.Idle)
        {
            Play(idleState);
            modeTimer -= Time.deltaTime;
            if (modeTimer <= 0f) { walkTarget = RandomPointAroundHome(); mode = Mode.Walk; }
        }
        else
        {
            Play(walkState);
            if (MoveTowards(walkTarget, walkSpeed)) { mode = Mode.Idle; modeTimer = Random.Range(idleTime.x, idleTime.y); }
        }
    }

    void Attack(Vector3 head)
    {
        mode = Mode.Attack;
        FaceDirection(head - transform.position);

        // Lancer en cours : le shuriken part au bon moment de l'animation
        if (throwAt > 0f)
        {
            if (Time.time >= throwAt) { Throw(head); throwAt = -1f; }
            return;
        }

        if (Time.time >= nextThrow)
        {
            Play(throwState, 0.1f);
            throwAt = Time.time + throwDelay;
            nextThrow = Time.time + fireInterval * Random.Range(0.8f, 1.2f);
        }
        else if (Time.time > nextThrow - fireInterval + 1.2f)
        {
            Play(idleState);   // l'animation de lancer est finie
        }
    }

    void Throw(Vector3 head)
    {
        Vector3 from = throwPoint != null
            ? throwPoint.position
            : transform.position + Vector3.up * 1.4f + Vector3.ProjectOnPlane(head - transform.position, Vector3.up).normalized * 0.5f;

        float dist = Vector3.Distance(from, head);
        Vector3 aim = head + Vector3.down * 0.15f + Random.insideUnitSphere * inaccuracy * (dist / 10f);
        Shuriken.Launch(projectilePrefab, from, (aim - from).normalized, projectileSpeed, damage, transform);
    }

    bool CanSee(Vector3 head)
    {
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 to = head - eye;
        foreach (var h in Physics.RaycastAll(eye, to.normalized, to.magnitude - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (PlayerHealth.Instance != null && h.collider.transform.IsChildOf(PlayerHealth.Instance.transform)) continue;
            if (h.collider.GetComponentInParent<ArrowHit>() != null) continue;
            return false;   // un mur, un arbre…
        }
        return true;
    }

    protected override string PopupText() => $"+{points}\n<size=45%>NINJA !</size>";
    protected override void OnRespawn() { mode = Mode.Idle; modeTimer = 2f; nextThrow = Time.time + fireInterval; }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
