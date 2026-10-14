using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// OUTIL DE TEST SANS CASQUE (à ne pas laisser dans la scène finale).
/// Clic gauche ou Espace dans la vue Game : tire la flèche là où pointe la souris.
/// N : manche suivante quand la manche est finie.
/// Clic sur le panneau des résultats : la caméra se place en face pour le lire (et cliquer ses boutons).
/// Clic droit ou Échap : retour à la position de tir (automatique quand une nouvelle manche commence).
/// Le calcul des points passe par le vrai chemin (ArrowHit → Target → ScoreManager).
/// </summary>
public class TestShooter : MonoBehaviour
{
    [Tooltip("La flèche de la scène (l'objet qui a ArrowHit).")]
    public ArrowHit arrow;

    [Tooltip("Vitesse du tir (l'arc tire entre 5 et 30 m/s).")]
    public float speed = 30f;

    [Tooltip("Vise un peu plus haut pour compenser la chute due à la gravité : la flèche arrive là où tu cliques.")]
    public bool compensateGravity = true;

    [Header("Vue du panneau")]
    [Tooltip("Distance entre la caméra et le panneau quand on clique dessus (m).")]
    public float boardViewDistance = 1.8f;

    Camera cam;
    Transform rig;                 // racine du joueur (XR Origin) : c'est elle qu'on déplace
    bool inBoardView;
    Vector3 savedRigPos;
    Quaternion savedRigRot;

    void Start()
    {
        cam = FindGameViewCamera();
        Debug.Log($"[TestShooter] Caméra utilisée pour viser : '{(cam != null ? cam.name : "aucune")}'.", cam);
        if (arrow == null) arrow = FindAnyObjectByType<ArrowHit>();
        if (arrow == null) Debug.LogError("[TestShooter] Aucune flèche avec ArrowHit dans la scène.");
        else Debug.Log("[TestShooter] Clic gauche ou Espace dans la vue Game pour tirer. Clic sur le panneau pour le lire, clic droit ou Échap pour revenir.");

        if (cam != null)
        {
            var origin = cam.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
            rig = origin != null ? origin.transform : cam.transform.root;
        }
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnRoundStarted += OnRoundStarted;
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnRoundStarted -= OnRoundStarted;
    }

    void OnRoundStarted(int round) { if (inBoardView) LeaveBoardView(); }

    void Update()
    {
        if (arrow == null || cam == null) return;

        var sm = ScoreManager.Instance;
        var mouse = Mouse.current;
        var kb = Keyboard.current;

        // Vue du panneau : clic droit / Échap pour revenir ; les clics gauches vont aux boutons du panneau
        if (inBoardView)
        {
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame))
                LeaveBoardView();
            else if (kb != null && kb.nKey.wasPressedThisFrame && sm != null && sm.IsRoundOver)
                sm.StartNextRound();
            return;
        }

        // Clic sur le panneau des résultats : on se place en face
        if (mouse != null && mouse.leftButton.wasPressedThisFrame
            && TryGetBoardUnderMouse(cam.ScreenPointToRay(mouse.position.ReadValue()), out RectTransform board))
        {
            EnterBoardView(board);
            return;
        }

        // N : manche suivante (raccourci de test)
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame && sm != null && sm.IsRoundOver)
        {
            sm.StartNextRound();
            return;
        }

        // Manche finie : on ne tire pas (le clic sert au bouton "Manche suivante")
        if (sm != null && (sm.IsRoundOver || sm.RoundStartFrame == Time.frameCount)) return;

        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (!click && !space) return;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width / 2f, Screen.height / 2f);

        Ray ray = cam.ScreenPointToRay(screenPos);
        Vector3 origin = ray.origin + ray.direction * 0.5f;
        Vector3 dir = ray.direction;

        // Point visé sous la souris (on ignore la flèche elle-même)
        if (compensateGravity && TryGetAimPoint(ray, out Vector3 aim))
        {
            float distance = Vector3.Distance(origin, aim);
            float time = distance / speed;
            float drop = 0.5f * -Physics.gravity.y * time * time;
            dir = (aim + Vector3.up * drop - origin).normalized;
            Debug.DrawLine(origin, aim, Color.cyan, 5f);
        }

        arrow.DebugLaunch(origin, dir, speed);
    }

    /// <summary>
    /// La caméra réellement affichée dans la vue Game : celle qui dessine à l'écran avec la plus grande "Depth".
    /// (Camera.main peut être une autre caméra si un pack en a ajouté une dans la scène.)
    /// </summary>
    static Camera FindGameViewCamera()
    {
        Camera best = null;
        foreach (var c in Camera.allCameras)
        {
            if (!c.isActiveAndEnabled || c.targetTexture != null || c.targetDisplay != 0) continue;
            if (best == null || c.depth > best.depth || (c.depth == best.depth && c == Camera.main)) best = c;
        }
        if (Camera.allCamerasCount > 1)
        {
            var names = new System.Text.StringBuilder();
            foreach (var c in Camera.allCameras) names.Append($" '{c.name}'");
            Debug.LogWarning($"[TestShooter] Plusieurs caméras actives dans la scène :{names}. Garde seulement celle du joueur (XR Origin).");
        }
        return best != null ? best : Camera.main;
    }

    // ---------- Vue du panneau ----------

    bool TryGetBoardUnderMouse(Ray ray, out RectTransform board)
    {
        board = null;
        float best = float.MaxValue;
        foreach (var rb in FindObjectsByType<ResultsBoard>(FindObjectsSortMode.None))
        {
            var canvas = rb.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = rb.GetComponentInChildren<Canvas>();
            if (canvas == null) continue;
            var rt = (RectTransform)canvas.transform;

            var plane = new Plane(rt.forward, rt.position);
            if (!plane.Raycast(ray, out float d) || d >= best) continue;

            Vector3 local = rt.InverseTransformPoint(ray.GetPoint(d));
            if (!rt.rect.Contains(new Vector2(local.x, local.y))) continue;
            best = d;
            board = rt;
        }
        return board != null;
    }

    void EnterBoardView(RectTransform board)
    {
        if (rig == null) return;
        savedRigPos = rig.position;
        savedRigRot = rig.rotation;

        // Un canvas World Space se lit en regardant dans le sens de son axe "forward"
        Vector3 center = board.TransformPoint(board.rect.center);
        Vector3 look = Vector3.ProjectOnPlane(board.forward, Vector3.up).normalized;
        if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
        Vector3 camTarget = center - look * boardViewDistance;

        // On tourne puis on déplace la racine du joueur pour que la caméra arrive à cet endroit
        Vector3 camFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        float yaw = Vector3.SignedAngle(camFlat, look, Vector3.up);
        rig.RotateAround(cam.transform.position, Vector3.up, yaw);
        rig.position += camTarget - cam.transform.position;
        Physics.SyncTransforms();   // pour que le CharacterController du joueur ne le remette pas en place

        inBoardView = true;
        Debug.Log("[TestShooter] Vue du panneau. Clic droit ou Échap pour revenir.");
    }

    void LeaveBoardView()
    {
        if (rig != null)
        {
            rig.position = savedRigPos;
            rig.rotation = savedRigRot;
            Physics.SyncTransforms();
        }
        inBoardView = false;
    }

    // ---------- Visée ----------

    bool TryGetAimPoint(Ray ray, out Vector3 point)
    {
        point = default;
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(arrow.transform)) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }

        // Les oiseaux bonus n'ont pas de collider : on vise le centre de celui qui est sous la souris
        foreach (var bird in BirdTarget.All)
        {
            if (bird == null || bird.IsHit) continue;
            Vector3 c = bird.Center;
            float t = Vector3.Dot(c - ray.origin, ray.direction);
            if (t <= 0f || t >= best) continue;
            if ((ray.origin + ray.direction * t - c).sqrMagnitude > bird.hitRadius * bird.hitRadius) continue;
            best = t;
            point = c;
        }
        return best < float.MaxValue;
    }
}
