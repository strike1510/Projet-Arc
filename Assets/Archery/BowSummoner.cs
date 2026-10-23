using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>
/// Sélecteur d'arcs : un bouton fait apparaître les arcs en cercle autour du joueur,
/// chacun avec son panneau de stats. Le joystick droit fait tourner le cercle.
/// Saisir un arc fait disparaître les autres ; réappuyer sur le bouton les fait tous disparaître.
/// Installation : glisser le prefab "Sélecteur d'arcs" dans la scène.
/// </summary>
public class BowSummoner : MonoBehaviour
{
    public List<BowData> bows = new List<BowData>();

    [Tooltip("Reprend les arcs déjà posés dans la scène (avec leurs textures et effets, ex. menu Projet Arc > Configurer) " +
             "au lieu de faire apparaître les prefabs. Le prefab du preset ne sert que si la scène n'a pas cet arc.")]
    public bool useSceneBows = true;

    [Tooltip("Cache les arcs de la scène que le sélecteur n'utilise pas (doublons), pour qu'il n'y ait pas deux fois le même arc.")]
    public bool hideUnusedSceneBows = true;

    [Header("Commandes (vide = réglages par défaut)")]
    [Tooltip("Ouvrir / fermer. Par défaut : bouton X (manette gauche) ou touche M.")]
    public InputActionProperty toggleInput;

    [Tooltip("Faire tourner les arcs (Vector2, on lit X). Par défaut : joystick droit.")]
    public InputActionProperty rotateInput;

    [Tooltip("Faire tourner les arcs au clavier (axe -1..1). Par défaut : flèches gauche / droite.")]
    public InputActionProperty rotateKeysInput;

    [Tooltip("Vitesse de rotation des arcs (degrés par seconde).")]
    public float rotateSpeed = 120f;

    [Tooltip("Coupe la rotation du joueur au joystick pendant que les arcs sont ouverts.")]
    public bool disableTurnWhileOpen = true;

    [Tooltip("Rotation lente du cercle tant que le joueur n'y touche pas (degrés par seconde, 0 = aucune). S'arrête dès qu'il le fait tourner.")]
    public float autoRotateSpeed = 8f;

    [Header("Disposition")]
    [Tooltip("Distance des arcs autour du joueur (m).")]
    public float radius = 0.8f;

    [Tooltip("Hauteur du centre des arcs sous les yeux du joueur (m).")]
    public float heightBelowHead = 0.25f;

    [Tooltip("Délai entre l'apparition de deux arcs (s).")]
    public float appearStagger = 0.06f;

    [Header("Panneaux")]
    [Tooltip("Position du panneau par rapport à l'arc : X = côté, Y = haut, Z = vers le joueur.")]
    public Vector3 panelOffset = new Vector3(0f, -0.42f, 0.22f);

    public float panelScale = 0.0005f;

    [Header("Test sans casque")]
    [Tooltip("Ajoute les touches de test au clavier dans l'éditeur, ou quand aucun casque n'est branché.")]
    public bool keyboardTest = true;

    public bool IsOpen { get; private set; }
    public int BowCount => spawned.Count;

    readonly List<FloatingBow> spawned = new List<FloatingBow>();
    readonly List<FloatingBow> displayed = new List<FloatingBow>();
    readonly List<Behaviour> disabledTurns = new List<Behaviour>();
    FloatingBow companionBeforeOpen;
    bool autoRotate;
    float baseYaw, ringYaw, centerY;
    Transform head;

    Transform Head
    {
        get
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            return head;
        }
    }

    void Awake()
    {
        if (toggleInput.action == null || toggleInput.action.bindings.Count == 0)
        {
            var a = new InputAction("Arcs", InputActionType.Button);
            a.AddBinding("<XRController>{LeftHand}/primaryButton");
            a.AddBinding("<Keyboard>/#(m)");   // la touche "m" quelle que soit la disposition (AZERTY ou QWERTY)
            toggleInput = new InputActionProperty(a);
        }
        if (rotateInput.action == null || rotateInput.action.bindings.Count == 0)
        {
            var a = new InputAction("Tourner les arcs", InputActionType.Value, expectedControlType: "Vector2");
            a.AddBinding("<XRController>{RightHand}/primary2DAxis");
            rotateInput = new InputActionProperty(a);
        }
        if (rotateKeysInput.action == null || rotateKeysInput.action.bindings.Count == 0)
        {
            var a = new InputAction("Tourner les arcs (clavier)", InputActionType.Value, expectedControlType: "Axis");
            a.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            rotateKeysInput = new InputActionProperty(a);
        }
    }

    void OnEnable()
    {
        toggleInput.action?.Enable();
        rotateInput.action?.Enable();
        rotateKeysInput.action?.Enable();
    }

    void OnDisable()
    {
        if (IsOpen) Close(null);
    }

    void Start()
    {
        // Les barres de stats sont relatives au meilleur arc
        float speedRef = 1f, drawRef = 0.01f;
        foreach (var d in bows)
        {
            if (d == null) continue;
            speedRef = Mathf.Max(speedRef, d.maxSpeed);
            drawRef = Mathf.Max(drawRef, d.maxDraw);
        }

        // Arcs déjà posés dans la scène, même désactivés
        var sceneBows = new List<BowController>();
        if (useSceneBows)
            sceneBows.AddRange(FindObjectsByType<BowController>(FindObjectsInactive.Include, FindObjectsSortMode.None));

        foreach (var d in bows)
        {
            if (d == null || d.prefab == null)
            {
                Debug.LogWarning("[BowSummoner] Un preset est vide ou n'a pas de prefab.", this);
                continue;
            }

            // L'arc de la scène qui correspond à ce preset (textures et effets déjà posés), sinon le prefab
            var go = TakeSceneBow(sceneBows, d.prefab.name);
            if (go == null)
            {
                go = Instantiate(d.prefab);
                go.name = d.prefab.name;
                Debug.Log($"[BowSummoner] Pas de {d.prefab.name} dans la scène : le prefab est utilisé (sans la configuration de la scène).", this);
            }
            d.ApplyTo(go.GetComponent<BowController>());

            var panel = BowInfoPanel.Create(d, panelScale, speedRef, drawRef);
            if (!go.TryGetComponent(out FloatingBow bow)) bow = go.AddComponent<FloatingBow>();
            bow.Init(this, d, panel);   // l'arc reste caché jusqu'au premier appui
            spawned.Add(bow);
        }

        // Les arcs de la scène qui restent (doublons) sont cachés
        if (hideUnusedSceneBows)
            foreach (var b in sceneBows)
                if (b != null && b.gameObject.activeSelf)
                {
                    b.gameObject.SetActive(false);
                    Debug.Log($"[BowSummoner] Arc en double caché : {b.name}.", b);
                }

        if (keyboardTest && (Application.isEditor || !UnityEngine.XR.XRSettings.isDeviceActive)
            && !TryGetComponent(out BowKeyboardTester _))
            gameObject.AddComponent<BowKeyboardTester>().summoner = this;
    }

    // Retire de la liste et renvoie l'arc de la scène qui correspond au prefab (même nom de préférence,
    // sinon même famille : "Bow_Bio" -> un objet dont le nom contient "Bio").
    static GameObject TakeSceneBow(List<BowController> candidates, string prefabName)
    {
        string family = prefabName.StartsWith("Bow_") ? prefabName.Substring(4) : prefabName;
        int found = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;
            string n = candidates[i].name;
            if (n.StartsWith(prefabName)) { found = i; break; }
            if (found < 0 && n.Contains(family)) found = i;
        }
        if (found < 0) return null;
        var go = candidates[found].gameObject;
        candidates.RemoveAt(found);
        return go;
    }

    /// <summary>L'arc n° index (dans l'ordre des presets), ou null.</summary>
    public FloatingBow GetBow(int index) => index >= 0 && index < spawned.Count ? spawned[index] : null;

    /// <summary>L'arc du cercle le plus en face du regard du joueur.</summary>
    public FloatingBow FrontBow()
    {
        if (!IsOpen || Head == null) return null;
        Vector3 look = Vector3.ProjectOnPlane(Head.forward, Vector3.up).normalized;
        FloatingBow best = null;
        float bestDot = float.MinValue;
        foreach (var b in displayed)
        {
            if (b == null || b.CurrentState != FloatingBow.State.InMenu) continue;
            Vector3 to = Vector3.ProjectOnPlane(b.transform.position - Head.position, Vector3.up).normalized;
            float dot = Vector3.Dot(look, to);
            if (dot > bestDot) { bestDot = dot; best = b; }
        }
        return best;
    }

    void Update()
    {
        if (toggleInput.action != null && toggleInput.action.WasPressedThisFrame())
        {
            if (IsOpen) Close(null);
            else Open();
        }

        if (!IsOpen) return;

        // Rotation du cercle : joystick droit ou flèches
        float x = 0f;
        if (rotateInput.action != null) x = rotateInput.action.ReadValue<Vector2>().x;
        if (Mathf.Abs(x) < 0.2f && rotateKeysInput.action != null) x = rotateKeysInput.action.ReadValue<float>();

        if (Mathf.Abs(x) >= 0.2f)
        {
            autoRotate = false;   // le joueur prend la main : la rotation automatique s'arrête
            ringYaw -= x * rotateSpeed * Time.deltaTime;   // pousser à droite amène l'arc de droite devant soi
        }
        else if (autoRotate)
        {
            ringYaw += autoRotateSpeed * Time.deltaTime;   // rotation lente tant que le joueur n'y touche pas
        }
        else if (displayed.Count > 0)
        {
            // Joystick relâché : le cercle se cale pour qu'un arc soit bien en face
            float step = 360f / displayed.Count;
            float target = Mathf.Round(ringYaw / step) * step;
            ringYaw = Mathf.MoveTowards(ringYaw, target, rotateSpeed * 0.75f * Time.deltaTime);
        }
    }

    public void Open()
    {
        if (IsOpen || Head == null) return;

        displayed.Clear();
        foreach (var b in spawned)
            if (b != null && b.CurrentState != FloatingBow.State.Held)
                displayed.Add(b);
        if (displayed.Count == 0) return;

        IsOpen = true;
        companionBeforeOpen = FloatingBow.NearPlayerBow;
        baseYaw = HeadYaw();
        ringYaw = 0f;
        autoRotate = autoRotateSpeed != 0f;
        centerY = Head.position.y - heightBelowHead;

        for (int i = 0; i < displayed.Count; i++)
            displayed[i].Summon(i, i * appearStagger);

        if (disableTurnWhileOpen) DisableTurning();
    }

    /// <summary>Ferme le sélecteur. chosen = l'arc saisi (null = annulé avec le bouton).</summary>
    public void Close(FloatingBow chosen)
    {
        if (!IsOpen) return;
        IsOpen = false;

        foreach (var b in displayed)
        {
            if (b == null || b == chosen || b.CurrentState != FloatingBow.State.InMenu) continue;

            // Annulé : l'arc qui flottait à côté du joueur y retourne
            if (chosen == null && b == companionBeforeOpen) b.FloatNearPlayer();
            else b.Dematerialize();
        }
        displayed.Clear();
        companionBeforeOpen = null;
        RestoreTurning();
    }

    /// <summary>Appelé par un arc quand le joueur le saisit.</summary>
    public void OnBowGrabbed(FloatingBow bow, bool wasInMenu)
    {
        if (IsOpen && wasInMenu) Close(bow);
    }

    /// <summary>Position d'un arc dans le cercle : axe de tir tangent au cercle, corde vers le haut.</summary>
    public void GetMenuPose(int slot, out Vector3 position, out Vector3 forward, out Vector3 inward)
    {
        int n = Mathf.Max(1, displayed.Count);
        Quaternion rot = Quaternion.Euler(0f, baseYaw + ringYaw + slot * 360f / n, 0f);
        Vector3 center = Head != null ? Head.position : transform.position;
        center.y = centerY;

        Vector3 outward = rot * Vector3.forward;
        position = center + outward * radius;
        forward = rot * Vector3.right;
        inward = -outward;
    }

    float HeadYaw()
    {
        Vector3 look = Vector3.ProjectOnPlane(Head.forward, Vector3.up);
        return look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look).eulerAngles.y : 0f;
    }

    void DisableTurning()
    {
        disabledTurns.Clear();
        foreach (var t in FindObjectsByType<SnapTurnProvider>(FindObjectsSortMode.None))
            if (t.enabled) { t.enabled = false; disabledTurns.Add(t); }
        foreach (var t in FindObjectsByType<ContinuousTurnProvider>(FindObjectsSortMode.None))
            if (t.enabled) { t.enabled = false; disabledTurns.Add(t); }
    }

    void RestoreTurning()
    {
        foreach (var t in disabledTurns)
            if (t != null) t.enabled = true;
        disabledTurns.Clear();
    }
}