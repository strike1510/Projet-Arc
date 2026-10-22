using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Comportement d'un arc du sélecteur (BowSummoner). Ajouté automatiquement : rien à mettre sur les prefabs.
///  - caché : n'existe pas dans la scène tant qu'on n'ouvre pas le sélecteur
///  - dans le cercle : flotte autour du joueur avec son panneau
///  - pris en main : arc normal (on peut encocher)
///  - lâché doucement : flotte à côté du joueur et le suit
///  - lancé (lâché vite) : file, rétrécit et disparaît
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(BowController))]
public class FloatingBow : MonoBehaviour
{
    public enum State { Hidden, InMenu, Held, NearPlayer, Vanishing }

    /// <summary>L'arc qui flotte actuellement à côté du joueur (un seul à la fois).</summary>
    public static FloatingBow NearPlayerBow { get; private set; }

    [Header("Lancer")]
    [Tooltip("Vitesse de la main (m/s, par rapport à la tête) au-delà de laquelle un lâcher compte comme un lancer.")]
    public float throwSpeed = 2.5f;

    [Tooltip("Durée de la disparition après un lancer (s).")]
    public float vanishDuration = 0.35f;

    [Header("Flotter à côté du joueur")]
    [Tooltip("Position par rapport à la tête : X = droite, Y = haut, Z = devant.")]
    public Vector3 sideOffset = new Vector3(0.35f, -0.5f, 0.2f);

    [Tooltip("Temps de rattrapage quand le joueur se déplace (s).")]
    public float followSmooth = 0.25f;

    [Header("Flottement")]
    public float bobAmplitude = 0.015f;
    public float bobSpeed = 1.6f;

    public State CurrentState { get; private set; } = State.Hidden;
    public BowData Data { get; private set; }

    BowSummoner summoner;
    BowInfoPanel panel;
    int menuSlot;
    XRGrabInteractable grab;
    BowController bowController;
    Transform head;

    Vector3 baseScale;
    Vector3 localForward = Vector3.forward;   // axe de tir dans le repère de l'arc
    Vector3 localUp = Vector3.up;             // axe de la corde dans le repère de l'arc
    Vector3 lastPos, lastHeadPos, velocity, followVel;
    float followYaw, phase;
    Coroutine anim;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        bowController = GetComponent<BowController>();
        baseScale = transform.localScale;
        phase = Random.value * 10f;

        grab.throwOnDetach = false;           // c'est nous qui décidons quoi faire au lâcher
        grab.retainTransformParent = false;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        // L'arc ne tombe jamais : il flotte (XRI remettra ces valeurs à chaque lâcher)
        if (TryGetComponent(out Rigidbody rb))
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // Axes de l'arc, pour pouvoir le présenter droit quel que soit le modèle
        if (bowController.arrowNock != null)
            localForward = transform.InverseTransformDirection(bowController.arrowNock.forward);
        var str = GetComponentInChildren<BowString>();
        if (str != null && str.stringTop != null && str.stringBottom != null)
            localUp = transform.InverseTransformDirection(str.stringTop.position - str.stringBottom.position);
        localUp = Vector3.ProjectOnPlane(localUp, localForward);
        if (localUp.sqrMagnitude < 1e-4f) localUp = Vector3.up;
        localForward.Normalize();
        localUp.Normalize();

        // Les arcs qui flottent autour du joueur ne doivent pas le bloquer
        var body = FindAnyObjectByType<CharacterController>();
        if (body != null)
            foreach (var c in GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(c, body);
    }

    void OnDestroy()
    {
        if (NearPlayerBow == this) NearPlayerBow = null;
        if (panel != null) Destroy(panel.gameObject);
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    /// <summary>Appelé par le sélecteur juste après l'apparition de l'arc : il reste caché.</summary>
    public void Init(BowSummoner owner, BowData data, BowInfoPanel infoPanel)
    {
        summoner = owner;
        Data = data;
        panel = infoPanel;
        SetState(State.Hidden);
        gameObject.SetActive(false);
    }

    // Rotation qui aligne l'axe de tir sur "forward" et la corde sur "up"
    Quaternion PoseRotation(Vector3 forward, Vector3 up) =>
        Quaternion.LookRotation(forward, up) * Quaternion.Inverse(Quaternion.LookRotation(localForward, localUp));

    Transform Head
    {
        get
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }
    }

    // ---------- Appelés par le sélecteur ----------

    /// <summary>Fait apparaître l'arc à sa place dans le cercle, après 'delay' secondes.</summary>
    public void Summon(int slot, float delay)
    {
        StopAnim();
        menuSlot = slot;
        bool wasHidden = CurrentState == State.Hidden || CurrentState == State.Vanishing || !gameObject.activeSelf;
        if (NearPlayerBow == this) NearPlayerBow = null;

        gameObject.SetActive(true);
        followVel = Vector3.zero;
        SetState(State.InMenu);

        if (wasHidden)
        {
            summoner.GetMenuPose(slot, out var pos, out var fwd, out _);
            transform.SetPositionAndRotation(pos, PoseRotation(fwd, Vector3.up));
            transform.localScale = Vector3.zero;
            anim = StartCoroutine(Materialize(delay));
        }
        else
        {
            transform.localScale = baseScale;   // l'arc qui flottait à côté du joueur rejoint le cercle
        }
        if (panel != null) panel.Show(delay + 0.1f);
    }

    /// <summary>Fait disparaître l'arc (choix d'un autre arc, ou sélecteur refermé).</summary>
    public void Dematerialize()
    {
        if (CurrentState == State.Hidden || CurrentState == State.Held) return;
        if (NearPlayerBow == this) NearPlayerBow = null;
        StopAnim();
        SetState(State.Vanishing);
        anim = StartCoroutine(Shrink(0.25f, Vector3.zero));
    }

    public void FloatNearPlayer()
    {
        if (NearPlayerBow != null && NearPlayerBow != this) NearPlayerBow.Dematerialize();
        NearPlayerBow = this;

        followVel = Vector3.zero;
        if (Head != null)
        {
            Vector3 look = Vector3.ProjectOnPlane(Head.forward, Vector3.up);
            if (look.sqrMagnitude > 1e-4f) followYaw = Quaternion.LookRotation(look).eulerAngles.y;
        }
        SetState(State.NearPlayer);
    }

    // ---------- Chaque image ----------

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 headPos = Head != null ? Head.position : Vector3.zero;
        float bob = Mathf.Sin((Time.time + phase) * bobSpeed) * bobAmplitude;

        switch (CurrentState)
        {
            case State.Held:
                // vitesse de la main par rapport à la tête (marcher en lâchant l'arc ne compte pas comme un lancer)
                Vector3 v = (transform.position - lastPos - (headPos - lastHeadPos)) / dt;
                velocity = Vector3.Lerp(velocity, v, 0.6f);
                break;

            case State.InMenu:
            {
                summoner.GetMenuPose(menuSlot, out var pos, out var fwd, out var inward);
                pos += Vector3.up * bob;
                MoveTowards(pos, PoseRotation(fwd, Vector3.up), 0.08f, dt);

                if (panel != null)
                {
                    Vector3 o = summoner.panelOffset;
                    panel.SetTarget(pos + fwd * o.x + Vector3.up * o.y + inward * o.z);
                }
                break;
            }

            case State.NearPlayer:
            {
                if (Head == null) break;
                Vector3 look = Vector3.ProjectOnPlane(Head.forward, Vector3.up);
                if (look.sqrMagnitude > 1e-4f)
                {
                    float headYaw = Quaternion.LookRotation(look).eulerAngles.y;
                    followYaw = Mathf.LerpAngle(followYaw, headYaw, 1f - Mathf.Exp(-2.5f * dt));   // suit la tête en douceur
                }
                Quaternion yaw = Quaternion.Euler(0f, followYaw, 0f);
                Vector3 target = headPos + yaw * sideOffset + Vector3.up * bob;
                MoveTowards(target, PoseRotation(yaw * Vector3.forward, Vector3.up), followSmooth, dt);
                break;
            }
        }

        lastPos = transform.position;
        lastHeadPos = headPos;
    }

    void MoveTowards(Vector3 pos, Quaternion rot, float smooth, float dt)
    {
        transform.SetPositionAndRotation(
            Vector3.SmoothDamp(transform.position, pos, ref followVel, smooth),
            Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-10f * dt)));
    }

    // ---------- Saisie / lâcher ----------

    void OnGrabbed(SelectEnterEventArgs args)
    {
        bool wasInMenu = CurrentState == State.InMenu;
        StopAnim();
        transform.localScale = baseScale;
        if (NearPlayerBow == this) NearPlayerBow = null;
        velocity = Vector3.zero;
        lastPos = transform.position;
        lastHeadPos = Head != null ? Head.position : Vector3.zero;
        SetState(State.Held);
        if (summoner != null) summoner.OnBowGrabbed(this, wasInMenu);
    }

    void OnReleased(SelectExitEventArgs args)
    {
        if (grab.isSelected) return;   // encore tenu par l'autre main

        if (velocity.magnitude >= throwSpeed)
        {
            SetState(State.Vanishing);
            StopAnim();
            anim = StartCoroutine(Shrink(vanishDuration, velocity));
        }
        else FloatNearPlayer();
    }

    // ---------- Animations ----------

    IEnumerator Materialize(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        EffectAssets.Sparkle(transform.position, Data != null ? Data.color : Color.white);

        const float duration = 0.4f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float back = 1f + 2.7f * Mathf.Pow(k - 1f, 3) + 1.7f * Mathf.Pow(k - 1f, 2);   // léger rebond
            transform.localScale = baseScale * back;
            yield return null;
        }
        transform.localScale = baseScale;
        anim = null;
    }

    // Rétrécit en tournant (et continue sur sa lancée si on l'a lancé), puis se cache
    IEnumerator Shrink(float duration, Vector3 v)
    {
        Vector3 startScale = transform.localScale;
        Vector3 spinAxis = Random.onUnitSphere;
        float spin = v.sqrMagnitude > 0.01f ? 900f : 360f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            transform.position += v * Time.deltaTime;
            v *= Mathf.Clamp01(1f - 4f * Time.deltaTime);
            transform.Rotate(spinAxis, spin * Time.deltaTime, Space.World);
            transform.localScale = startScale * (1f - k * k);
            yield return null;
        }
        EffectAssets.Sparkle(transform.position, Data != null ? Data.color : Color.white);
        anim = null;
        Hide();
    }

    void Hide()
    {
        transform.localScale = baseScale;
        SetState(State.Hidden);
        gameObject.SetActive(false);
    }

    void StopAnim()
    {
        if (anim != null) StopCoroutine(anim);
        anim = null;
    }

    void SetState(State s)
    {
        CurrentState = s;
        // on ne peut encocher que sur l'arc tenu en main
        bowController.enabled = s == State.Held;
        if (s != State.InMenu && panel != null) panel.Hide();
    }
}
