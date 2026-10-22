using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// OUTIL DE TEST SANS CASQUE (à ne pas laisser dans la scène finale).
/// Tir instantané (pas de flèche, pas de physique) : ce qui est sous la souris est touché.
///   Clic gauche ou Espace : tire là où pointe la souris (cible → points au-dessus, oiseau/pomme → bonus).
///   N : manche suivante quand la manche est finie.
///   B (ou 1 / 2 / 3 / 4) : choisit l'arc (Base, Bio, Fulminaleuse, Monkey) ; F : tire une VRAIE flèche avec cet arc
///   vers la souris (traînée de l'arc, tir triple du Bow_Monkey, points comptés).
///   Clic sur le panneau des résultats : la caméra se place en face pour le lire (et cliquer ses boutons).
///   Clic droit ou Échap : retour à la position de tir (automatique quand une nouvelle manche commence).
/// Les points passent par le vrai chemin : Target / BirdTarget → ScoreManager.
/// </summary>
public class TestShooter : MonoBehaviour
{
    [Tooltip("Portée du tir (m).")]
    public float range = 200f;

    [Tooltip("Affiche un petit trait jaune pendant un instant pour voir le tir.")]
    public bool showTracer = true;

    [Header("Vue du panneau")]
    [Tooltip("Distance entre la caméra et le panneau quand on clique dessus (m).")]
    public float boardViewDistance = 1.8f;

    Camera cam;
    Transform rig;                 // racine du joueur (XR Origin) : c'est elle qu'on déplace
    bool inBoardView;
    Vector3 savedRigPos;
    Quaternion savedRigRot;
    Material tracerMaterial;

    void Start()
    {
        cam = FindGameViewCamera();
        Debug.Log($"[TestShooter] Tir instantané avec la caméra '{(cam != null ? cam.name : "aucune")}'. Clic gauche pour tirer, clic sur le panneau pour le lire, B (ou 1-4) + F pour une vraie flèche.", cam);

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
        if (cam == null || !cam.isActiveAndEnabled) cam = FindGameViewCamera();
        if (cam == null) return;

        var sm = ScoreManager.Instance;
        var mouse = Mouse.current;
        var kb = Keyboard.current;

        // 1-4 : arc de test, F : vraie flèche
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) SelectBow(ArcheryTypes.Base);
            if (kb.digit2Key.wasPressedThisFrame) SelectBow(ArcheryTypes.Bio);
            if (kb.digit3Key.wasPressedThisFrame) SelectBow(ArcheryTypes.Fulminaleuse);
            if (kb.digit4Key.wasPressedThisFrame) SelectBow(ArcheryTypes.Monkey);
            if (kb.bKey.wasPressedThisFrame)
                SelectBow(ArcheryTypes.All[(System.Array.IndexOf(ArcheryTypes.All, testBowType) + 1) % ArcheryTypes.All.Length]);
            if (kb.fKey.wasPressedThisFrame && !inBoardView)
            {
                Vector2 sp = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
                FireRealArrow(cam.ScreenPointToRay(sp));
            }
        }

        // N : manche suivante
        if (kb != null && kb.nKey.wasPressedThisFrame && sm != null && sm.IsRoundOver)
        {
            sm.StartNextRound();
            return;
        }

        // Vue du panneau : clic droit / Échap pour revenir ; les clics gauches vont aux boutons du panneau
        if (inBoardView)
        {
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame))
                LeaveBoardView();
            return;
        }

        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool space = kb != null && kb.spaceKey.wasPressedThisFrame;
        if (!click && !space) return;

        Vector2 screenPos = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = cam.ScreenPointToRay(screenPos);

        // Clic sur le panneau des résultats : on se place en face
        if (click && TryGetBoardUnderMouse(ray, out RectTransform board))
        {
            EnterBoardView(board);
            return;
        }

        // Manche finie : on ne tire pas (le clic sert aux boutons)
        if (sm != null && (sm.IsRoundOver || sm.RoundStartFrame == Time.frameCount)) return;

        Shoot(ray);
    }

    // ---------- Vraie flèche (test des arcs sans casque) ----------

    string testBowType = ArcheryTypes.Base;

    void SelectBow(string type)
    {
        testBowType = type;
        Debug.Log($"[TestShooter] Arc de test : {type} (F pour tirer).");
    }

    void FireRealArrow(Ray ray)
    {
        BowController bow = null;
        foreach (var b in BowController.All) if (ArcheryTypes.Of(b.name) == testBowType) { bow = b; break; }
        if (bow == null) { Debug.Log($"[TestShooter] Pas d'arc {testBowType} dans la scène."); return; }

        // Une flèche du même type, libre (pas en main, pas en vol, pas une flèche latérale du tir triple)
        ArrowController pick = null;
        foreach (var a in FindObjectsByType<ArrowController>(FindObjectsInactive.Include))
        {
            if (ArcheryTypes.Of(a.name) != testBowType || a.name.Contains("tir triple")) continue;
            if (a.IsHeld || a.IsNocked) continue;
            var h = a.GetComponent<ArrowHit>();
            if (h == null || h.IsFlying) continue;
            pick = a;
            if (!a.gameObject.activeInHierarchy) break;   // de préférence une flèche du carquois
        }
        if (pick == null) { Debug.Log($"[TestShooter] Aucune flèche {testBowType} libre."); return; }

        if (!pick.gameObject.activeInHierarchy)
        {
            pick.transform.SetParent(null, true);
            pick.gameObject.SetActive(true);
        }
        pick.bow = bow;   // la traînée prend le style de cet arc

        Vector3 origin = ray.origin + ray.direction * 0.6f + cam.transform.right * 0.12f - cam.transform.up * 0.12f;
        Vector3 dir = ray.direction;
        float speed = bow.maxSpeed;
        pick.GetComponent<ArrowHit>().DebugLaunch(origin, dir, speed);
        bow.NotifyFired(pick, dir, speed, origin);
    }

    // ---------- Tir instantané ----------

    void Shoot(Ray ray)
    {
        var sm = ScoreManager.Instance;
        if (sm != null && !sm.RegisterShot()) return;   // plus de flèches

        // Ce qui est touché en premier : un collider, ou la zone d'un oiseau/pomme (qui n'ont pas toujours de collider)
        bool hasHit = TryRaycast(ray, out RaycastHit hit);
        float hitDist = hasHit ? hit.distance : range;

        BirdTarget bird = hasHit ? hit.collider.GetComponentInParent<BirdTarget>() : null;
        Vector3 point = hasHit ? hit.point : ray.GetPoint(range);
        if (bird != null && bird.IsHit) bird = null;

        if (bird == null && BirdTarget.FindHit(ray.origin, ray.GetPoint(hitDist), out BirdTarget zoneBird, out Vector3 zonePoint, out float _))
        {
            bird = zoneBird;
            point = zonePoint;
        }

        if (showTracer) Tracer(ray.origin + cam.transform.up * -0.15f + cam.transform.right * 0.1f, point);

        if (bird != null)
        {
            bird.OnArrowHit(point, ray.direction);
            return;
        }

        var target = hasHit ? hit.collider.GetComponentInParent<Target>() : null;
        if (target != null)
        {
            target.OnArrowHit(point);
            return;
        }

        Debug.Log(hasHit ? $"[TestShooter] Raté : touché '{hit.collider.name}'." : "[TestShooter] Raté : rien touché.");
        if (sm != null) sm.RegisterMiss();
    }

    bool TryRaycast(Ray ray, out RaycastHit best)
    {
        best = default;
        bool found = false;
        foreach (var h in Physics.RaycastAll(ray, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.GetComponentInParent<ArrowHit>() != null) continue;      // les flèches posées dans la scène
            if (rig != null && h.collider.transform.IsChildOf(rig)) continue;       // le joueur lui-même
            if (!found || h.distance < best.distance) { best = h; found = true; }
        }
        return found;
    }

    void Tracer(Vector3 from, Vector3 to)
    {
        if (tracerMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            tracerMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            tracerMaterial.color = new Color(1f, 0.85f, 0.2f);
        }
        var go = new GameObject("Tir");
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = tracerMaterial;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = 0.02f;
        lr.endWidth = 0.05f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go, 0.12f);
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
        return best != null ? best : Camera.main;
    }

    // ---------- Vue du panneau ----------

    bool TryGetBoardUnderMouse(Ray ray, out RectTransform board)
    {
        board = null;
        float best = float.MaxValue;
        foreach (var rb in FindObjectsByType<ResultsBoard>())
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
}
