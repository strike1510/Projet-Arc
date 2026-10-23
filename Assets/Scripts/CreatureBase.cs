using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Base commune des ninjas et des animaux : marche au sol, animations par nom d'état, collider automatique,
/// mort / disparition / réapparition. Ne s'utilise pas seul : mettre Ninja ou Animal sur le personnage.
///
/// Animations : l'Animator du personnage doit avoir des états portant ces noms (Idle, Walk, Run…).
/// Pas besoin de flèches de transition : le script passe d'un état à l'autre tout seul.
/// </summary>
public abstract class CreatureBase : MonoBehaviour, IArrowHittable
{
    [Header("Vie")]
    [Tooltip("Nombre de flèches pour l'abattre.")]
    public int health = 1;

    [Tooltip("Points affichés quand on l'abat.")]
    public int points = 50;

    [Tooltip("Réapparaît à son point de départ après un moment.")]
    public bool respawn = true;

    [Tooltip("Délai avant de réapparaître (s).")]
    public float respawnDelay = 20f;

    [Tooltip("Écrit dans la Console ce que fait la créature (pour comprendre si elle ne bouge pas).")]
    public bool debugLog = false;

    [Header("Déplacement")]
    [Tooltip("Vitesse de marche (m/s).")]
    public float walkSpeed = 1.5f;

    [Tooltip("Rayon de balade autour de son point de départ (m).")]
    public float wanderRadius = 10f;

    [Tooltip("Pause entre deux balades (s), au hasard entre ces deux valeurs.")]
    public Vector2 idleTime = new Vector2(2f, 5f);

    [Tooltip("Rotation ajoutée si le modèle avance de côté ou à reculons (90, 180, -90).")]
    public float modelYawOffset = 0f;

    [Header("Noms des états de l'Animator (vide = pas d'animation)")]
    public string idleState = "Idle";
    public string walkState = "Walk";
    public string dieState = "Die";

    [Header("Ou bien : Blend Tree (animaux ithappy…) — rempli tout seul, voir \"Détecter les animations\"")]
    [Tooltip("Paramètre Float principal du Blend Tree.")]
    public string speedParameter = "";
    [Tooltip("Valeurs de ce paramètre pour : immobile, marche, course.")]
    public float idleValue = 0f, walkValue = 0.5f, runValue = 1f;
    [Tooltip("Second paramètre (Blend Tree 2D seulement), vide sinon.")]
    public string speedParameterY = "";
    public float idleValueY = 0f, walkValueY = 0f, runValueY = 0f;
    [HideInInspector] public bool animationsDetected;

    protected Animator anim;
    protected Vector3 home;
    protected Quaternion homeRotation;
    protected int hp;
    protected bool dead;
    protected Camera cam;
    string currentState;
    Collider[] colliders;
    Renderer[] renderers;

    public bool CanBeHit => !dead;
    public bool IsDead => dead;

    protected virtual void Awake()
    {
#if UNITY_EDITOR
        if (!animationsDetected) DetectAnimations();
#endif
        anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.applyRootMotion = false;   // c'est le script qui déplace le personnage
        home = transform.position;
        homeRotation = transform.rotation;
        hp = health;

        foreach (var rb in GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; rb.useGravity = false; }
        DisableOtherMovers();
        EnsureCollider();
        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
    }

    protected virtual void Start()
    {
        cam = TargetSpawner.FindGameViewCamera();
        Play(idleState);
    }

    /// <summary>
    /// Les packs d'animaux/personnages ont souvent leur propre script de déplacement (démo au clavier, "Mover",
    /// "Input"…) qui bloque le personnage ou écrase ses animations. On les coupe : c'est ce script qui le pilote.
    /// </summary>
    void DisableOtherMovers()
    {
        foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb is CreatureBase || !mb.enabled) continue;
            string n = mb.GetType().Name;
            if (n.Contains("Mover") || n.Contains("Movement") || n.Contains("Input") || n.Contains("Locomotion")
                || n.Contains("PlayerController") || n.Contains("CharacterControl") || n.Contains("AIController") || n.Contains("Wander"))
            {
                mb.enabled = false;
                Debug.Log($"[{GetType().Name}] '{name}' : script '{n}' du pack désactivé (il bloquait le déplacement).", this);
            }
        }
    }

    /// <summary>Ajoute une capsule à la taille du modèle s'il n'a aucun collider (pour que les flèches le touchent).</summary>
    void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null) return;
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        var cap = gameObject.AddComponent<CapsuleCollider>();
        Vector3 localCenter = transform.InverseTransformPoint(b.center);
        float scale = Mathf.Max(0.0001f, transform.lossyScale.y);
        cap.center = localCenter;
        cap.height = b.size.y / scale;
        cap.radius = Mathf.Max(b.size.x, b.size.z) * 0.5f / scale;
        cap.direction = b.size.y >= Mathf.Max(b.size.x, b.size.z) ? 1 : (b.size.x > b.size.z ? 0 : 2);   // animaux : couchée
    }

    // ---------- Animations ----------

    protected void Play(string state, float fade = 0.15f)
    {
        if (string.IsNullOrEmpty(state) || currentState == state) return;
        if (debugLog) Debug.Log($"[{GetType().Name}] '{name}' → {state}", this);
        if (anim == null) { currentState = state; return; }
        int hash = Animator.StringToHash(state);
        if (anim.HasState(0, hash))
        {
            anim.CrossFadeInFixedTime(hash, fade, 0);
            currentState = state;
            return;
        }

        // Pas d'état à ce nom : on joue directement l'animation trouvée par son nom (idle, walk, run…)
        if (PlayClipFor(state)) { currentState = state; return; }

        // Sinon : on pilote le Blend Tree avec son (ses) paramètre(s)
        string param = SpeedParam();
        if (param == null) return;
        float x, y;
        if (state == idleState) { x = idleValue; y = idleValueY; }
        else if (state == walkState) { x = walkValue; y = walkValueY; }
        else if (IsRunState(state)) { x = runValue; y = runValueY; }
        else return;
        targetX = x; targetY = y;
        currentState = state;
    }

    float targetX, targetY;

    // ---------- Lecture directe des animations (sans Animator Controller) ----------

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable[] slots = new AnimationClipPlayable[2];
    AnimationClip[] slotClips = new AnimationClip[2];
    int activeSlot;
    float blend = 1f;
    bool clipsScanned;
    AnimationClip clipIdle, clipWalk, clipRun, clipDie;

    static readonly string[] idleWords = { "idle", "stand", "breath", "rest" };
    static readonly string[] walkWords = { "walk" };
    static readonly string[] runWords = { "run", "gallop", "trot", "sprint", "canter" };
    static readonly string[] dieWords = { "death", "die", "dead", "dying" };

    void ScanClips()
    {
        clipsScanned = true;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        var clips = anim.runtimeAnimatorController.animationClips;
        clipIdle = FindClip(clips, idleWords);
        clipWalk = FindClip(clips, walkWords);
        clipRun = FindClip(clips, runWords);
        clipDie = FindClip(clips, dieWords);
        if (clipWalk == null && clipRun == null && clipIdle == null) return;

        graph = PlayableGraph.Create(name + "_anim");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        AnimationPlayableOutput.Create(graph, "Anim", anim).SetSourcePlayable(mixer);
        graph.Play();
        Debug.Log($"[{GetType().Name}] '{name}' : animations repos={Name(clipIdle)}, marche={Name(clipWalk)}, course={Name(clipRun)}.", this);
    }

    static string Name(AnimationClip c) => c != null ? c.name : "—";

    static AnimationClip FindClip(AnimationClip[] clips, string[] words)
    {
        foreach (var w in words)
            foreach (var c in clips)
                if (c != null && c.name.ToLowerInvariant().Contains(w)) return c;
        return null;
    }

    bool PlayClipFor(string state)
    {
        if (!clipsScanned) ScanClips();
        if (!graph.IsValid()) return false;

        AnimationClip clip = null;
        if (state == idleState) clip = clipIdle != null ? clipIdle : clipWalk;
        else if (state == walkState) clip = clipWalk != null ? clipWalk : clipRun;
        else if (IsRunState(state)) clip = clipRun != null ? clipRun : clipWalk;
        else if (state == dieState) clip = clipDie != null ? clipDie : clipIdle;
        if (clip == null) return false;
        if (slotClips[activeSlot] == clip) return true;

        // Nouvelle animation dans l'autre emplacement, puis fondu de 0,2 s
        int next = 1 - activeSlot;
        if (slots[next].IsValid()) { graph.Disconnect(mixer, next); slots[next].Destroy(); }
        slots[next] = AnimationClipPlayable.Create(graph, clip);
        slotClips[next] = clip;
        graph.Connect(slots[next], 0, mixer, next);
        activeSlot = next;
        blend = 0f;
        return true;
    }

    void UpdateClipPlayer()
    {
        if (!graph.IsValid()) return;
        blend = Mathf.MoveTowards(blend, 1f, Time.deltaTime / 0.2f);
        mixer.SetInputWeight(activeSlot, slots[activeSlot].IsValid() ? blend : 0f);
        mixer.SetInputWeight(1 - activeSlot, slots[1 - activeSlot].IsValid() ? 1f - blend : 0f);

        // Les animations qui ne bouclent pas toutes seules : on les fait boucler (sauf la mort)
        for (int i = 0; i < 2; i++)
        {
            if (!slots[i].IsValid() || slotClips[i] == null || slotClips[i].isLooping || slotClips[i] == clipDie) continue;
            double len = slotClips[i].length;
            if (len > 0 && slots[i].GetTime() >= len) slots[i].SetTime(slots[i].GetTime() % len);
        }
    }

    protected virtual void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    protected virtual void LateUpdate()
    {
        UpdateClipPlayer();
        // Passage en douceur d'une valeur à l'autre (pas de saut entre marche et course)
        if (anim == null || cachedParam == null || cachedParam.Length == 0) return;
        anim.SetFloat(cachedParam, targetX, 0.15f, Time.deltaTime);
        if (!string.IsNullOrEmpty(speedParameterY)) anim.SetFloat(speedParameterY, targetY, 0.15f, Time.deltaTime);
    }

    protected virtual bool IsRunState(string state) => false;

    string cachedParam;
    string SpeedParam()
    {
        if (cachedParam != null) return cachedParam.Length > 0 ? cachedParam : null;
        cachedParam = "";
        if (anim == null || anim.runtimeAnimatorController == null) return null;
        foreach (var p in anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Float) continue;
            if (string.IsNullOrEmpty(speedParameter) || p.name == speedParameter) { cachedParam = p.name; break; }
        }
        if (cachedParam.Length == 0) Debug.LogWarning($"[{GetType().Name}] '{name}' : pas d'état '{idleState}' ni de paramètre Float dans l'Animator.", this);
        return cachedParam.Length > 0 ? cachedParam : null;
    }

    // ---------- Déplacement ----------

    /// <summary>Avance vers 'target' au sol. Renvoie vrai une fois arrivé.</summary>
    protected bool MoveTowards(Vector3 target, float speed)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.3f) return true;

        Vector3 step = to.normalized * Mathf.Min(speed * Time.deltaTime, to.magnitude);
        Vector3 next = transform.position + step;

        // Obstacle devant (arbre, rocher, maison, buisson) : on s'arrête là, il choisira une autre direction.
        // On "tâte" avec une sphère de la largeur de l'animal, à mi-hauteur.
        float radius = BodyRadius();
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.3f, radius);
        foreach (var wall in Physics.SphereCastAll(origin, radius * 0.8f, to.normalized, radius + 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (wall.collider.transform.IsChildOf(transform)) continue;
            if (wall.collider is TerrainCollider) continue;                          // une pente, pas un mur
            if (wall.collider.GetComponentInParent<CreatureBase>() != null) continue;  // un autre animal
            if (wall.collider.GetComponentInParent<ArrowHit>() != null) continue;
            if (wall.distance <= 0f) continue;                                       // déjà collé dedans : on le laisse sortir
            if (wall.normal.y > 0.6f) continue;                                      // une petite bosse
            return true;
        }

        next.y = GroundY(next, transform.position.y);
        transform.position = next;
        FaceDirection(to);
        return false;
    }

    protected void FaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        Quaternion want = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, modelYawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, 8f * Time.deltaTime);
    }

    protected float GroundY(Vector3 p, float fallback)
    {
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<CreatureBase>() != null) continue;
            if (h.collider.GetComponentInParent<ArrowHit>() != null) continue;
            return h.point.y;
        }
        return fallback;
    }

    protected Vector3 RandomPointAroundHome()
    {
        // Un point libre (pas au milieu d'un buisson ou d'un arbre), sinon on reste sur place
        float radius = BodyRadius();
        for (int i = 0; i < 10; i++)
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            Vector3 p = home + new Vector3(r.x, 0f, r.y);
            p.y = GroundY(p, transform.position.y);
            bool blocked = false;
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * (radius + 0.2f), radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider || c.transform.IsChildOf(transform) || c.GetComponentInParent<CreatureBase>() != null) continue;
                blocked = true; break;
            }
            if (!blocked) return p;
        }
        return transform.position;
    }

    float bodyRadius = -1f;
    /// <summary>Demi-largeur de l'animal (m).</summary>
    protected float BodyRadius()
    {
        if (bodyRadius > 0f) return bodyRadius;
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return bodyRadius = 0.3f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return bodyRadius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.5f, 0.15f, 1.2f);
    }

    protected Vector3 PlayerHead()
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = TargetSpawner.FindGameViewCamera();
        return cam != null ? cam.transform.position : transform.position + transform.forward * 100f;
    }

    // ---------- Touché / mort ----------

    public virtual void OnArrowHit(Vector3 point, Vector3 direction)
    {
        if (dead) return;
        hp--;
        if (hp > 0) { OnHurt(direction); return; }

        dead = true;
        StopAllCoroutines();
        HitPopup.Show(point + Vector3.up * 0.5f, PopupText(), PopupColor());
        StartCoroutine(DieRoutine(direction));
    }

    protected virtual void OnHurt(Vector3 direction) { }
    protected virtual string PopupText() => $"+{points}";
    protected virtual Color PopupColor() => new Color(1f, 0.84f, 0f);

    /// <summary>Par défaut : animation de mort, puis il disparaît. Animal la remplace par une fuite.</summary>
    protected virtual IEnumerator DieRoutine(Vector3 direction)
    {
        Play(dieState, 0.1f);
        foreach (var c in colliders) if (c != null) c.enabled = false;
        yield return new WaitForSeconds(3f);
        yield return HideAndRespawn();
    }

    protected IEnumerator HideAndRespawn()
    {
        // Les flèches plantées dedans tombent
        foreach (var arrow in GetComponentsInChildren<ArrowHit>(true)) arrow.Unstick();
        foreach (var c in colliders) if (c != null) c.enabled = false;
        foreach (var r in renderers) if (r != null) r.enabled = false;
        if (!respawn) yield break;

        yield return new WaitForSeconds(respawnDelay);
        transform.SetPositionAndRotation(home, homeRotation);
        foreach (var r in renderers) if (r != null) r.enabled = true;
        foreach (var c in colliders) if (c != null) c.enabled = true;
        hp = health;
        dead = false;
        currentState = null;
        Play(idleState, 0f);
        OnRespawn();
    }

    protected virtual void OnRespawn() { }

    /// <summary>Nouveau point de départ (utilisé par AnimalHerd pour faire réapparaître ailleurs).</summary>
    public void SetHome(Vector3 position) { home = position; homeRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f); }

    /// <summary>Taille du modèle (plus grande dimension), pour donner plus de vie aux gros animaux.</summary>
    public float ModelSize()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 1f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return Mathf.Max(b.size.x, b.size.y, b.size.z);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Lit le Blend Tree de l'Animator (dans l'éditeur) et remplit tout seul le paramètre et les valeurs
    /// immobile / marche / course, d'après le nom des animations (idle, walk, run, gallop, trot…).
    /// </summary>
    [ContextMenu("Détecter les animations")]
    public void DetectAnimations()
    {
        var a = GetComponentInChildren<Animator>();
        var ctrl = a != null ? a.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
        if (ctrl == null) return;

        foreach (var layer in ctrl.layers)
        foreach (var st in layer.stateMachine.states)
        {
            if (!(st.state.motion is UnityEditor.Animations.BlendTree tree)) continue;
            bool is2D = tree.blendType != UnityEditor.Animations.BlendTreeType.Simple1D;
            bool foundIdle = false, foundWalk = false, foundRun = false;

            foreach (var child in tree.children)
            {
                string n = child.motion != null ? child.motion.name.ToLowerInvariant() : "";
                float x = is2D ? child.position.x : child.threshold;
                float y = is2D ? child.position.y : 0f;
                if (!foundIdle && (n.Contains("idle") || n.Contains("stand") || n.Contains("breath"))) { idleValue = x; idleValueY = y; foundIdle = true; }
                else if (!foundWalk && n.Contains("walk")) { walkValue = x; walkValueY = y; foundWalk = true; }
                else if (!foundRun && (n.Contains("run") || n.Contains("gallop") || n.Contains("trot") || n.Contains("sprint"))) { runValue = x; runValueY = y; foundRun = true; }
            }
            if (!foundIdle && !foundWalk && !foundRun) continue;

            // Ce qui manque : on prend les seuils dans l'ordre (le plus petit = immobile, le plus grand = course)
            if (!is2D && tree.children.Length > 0)
            {
                float min = float.MaxValue, max = float.MinValue;
                foreach (var c in tree.children) { min = Mathf.Min(min, c.threshold); max = Mathf.Max(max, c.threshold); }
                if (!foundIdle) idleValue = min;
                if (!foundRun) runValue = max;
                if (!foundWalk) walkValue = (idleValue + runValue) * 0.5f;
            }

            speedParameter = tree.blendParameter;
            speedParameterY = is2D ? tree.blendParameterY : "";
            animationsDetected = true;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{GetType().Name}] '{name}' : Blend Tree « {speedParameter} » — immobile {idleValue}, marche {walkValue}, course {runValue}.", this);
            return;
        }
    }
#endif

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadius);
    }
}
