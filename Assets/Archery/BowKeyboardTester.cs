using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// TEST SANS CASQUE : touches clavier pour essayer le sélecteur d'arcs.
/// Ajouté automatiquement par BowSummoner dans l'éditeur (ou sans casque branché).
/// L'arc "tenu" se place devant la caméra et vise la souris.
/// Les lettres suivent la disposition du clavier (AZERTY ou QWERTY).
/// </summary>
public class BowKeyboardTester : MonoBehaviour
{
    public BowSummoner summoner;

    [Tooltip("Position de l'arc tenu, par rapport à la caméra : X = droite, Y = haut, Z = devant.")]
    public Vector3 handOffset = new Vector3(0.22f, -0.18f, 0.6f);

    [Tooltip("Vitesse de la main quand on lance l'arc avec T (m/s).")]
    public float throwSpeed = 6f;

    [Tooltip("Les flèches tirées avec F disparaissent au bout de ce temps (s).")]
    public float arrowLifetime = 20f;

    FloatingBow held;
    MonoBehaviour shooter;   // le TestShooter de la scène (s'il y en a un) : il tire déjà de vraies flèches avec F
    bool showHelp = true;
    string lastMessage = "";
    float messageUntil;

    void Start()
    {
        // Cherché par son nom : ce script marche aussi si TestShooter est retiré du projet
        foreach (var m in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (m.GetType().Name == "TestShooter") { shooter = m; break; }
    }

    void Update()
    {
        var kb = Keyboard.current;
        var cam = Camera.main;
        if (kb == null || cam == null || summoner == null) return;

        if (Pressed(kb, "h")) showHelp = !showHelp;

        // L'arc a pu être lâché autrement (manette, lancer...) : on l'oublie
        if (held != null && held.CurrentState != FloatingBow.State.Held) held = null;

        if (summoner.IsOpen)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Take(summoner.FrontBow());
            for (int i = 0; i < Mathf.Min(9, summoner.BowCount); i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame || kb[Key.Numpad1 + i].wasPressedThisFrame)
                    Take(summoner.GetBow(i));
        }
        else if (Pressed(kb, "r") && held == null && FloatingBow.NearPlayerBow != null)
        {
            // Reprendre l'arc qui flotte à côté du joueur
            held = FloatingBow.NearPlayerBow;
            held.DebugGrab();
            SyncTestShooter();
            Say("Arc repris : " + held.Data.displayName);
        }

        if (held == null) return;

        Vector3 hand = cam.transform.TransformPoint(handOffset);
        Vector3 aim = AimDirection(cam, hand);
        Vector3 up = Vector3.ProjectOnPlane(cam.transform.up, aim);
        held.SetHeldPose(hand, aim, up.sqrMagnitude > 1e-4f ? up : Vector3.up);

        if (Pressed(kb, "f"))
        {
            // Le TestShooter de la scène tire déjà avec F (avec l'arc en main) : on ne tire pas une 2e fois
            if (shooter == null || !shooter.isActiveAndEnabled) Fire(aim);
        }
        else if (Pressed(kb, "g"))
        {
            held.DebugRelease(Vector3.zero);
            Say("Arc lâché : il flotte à côté de toi (R pour le reprendre)");
            held = null;
        }
        else if (Pressed(kb, "t"))
        {
            held.DebugRelease(aim * throwSpeed);
            Say("Arc lancé : il disparaît");
            held = null;
        }
    }

    static bool Pressed(Keyboard kb, string character)
    {
        KeyControl key = kb.FindKeyOnCurrentKeyboardLayout(character);
        return key != null && key.wasPressedThisFrame;
    }

    void Take(FloatingBow bow)
    {
        if (bow == null || bow.CurrentState != FloatingBow.State.InMenu) return;

        // On ne tient qu'un arc à la fois au clavier : l'ancien disparaît
        if (held != null)
        {
            var old = held;
            old.DebugRelease(Vector3.zero);
            old.Dematerialize();
        }
        held = bow;
        held.DebugGrab();   // ferme le sélecteur et fait disparaître les autres arcs
        SyncTestShooter();
        Say("Arc en main : " + held.Data.displayName + " (F pour tirer vers la souris)");
    }

    // Le TestShooter tire avec le type d'arc qu'on lui a choisi : on lui donne celui qu'on tient
    void SyncTestShooter()
    {
        if (shooter != null && held != null)
            shooter.SendMessage("SelectBow", ArcheryTypes.Of(held.name), SendMessageOptions.DontRequireReceiver);
    }

    // Direction de tir : vers ce qui est sous la souris
    Vector3 AimDirection(Camera cam, Vector3 from)
    {
        var mouse = Mouse.current;
        Vector2 screen = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = cam.ScreenPointToRay(screen);

        Vector3 point = ray.GetPoint(60f);
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(ray, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.distance >= best) continue;
            if (held != null && h.collider.transform.IsChildOf(held.transform)) continue;
            if (h.collider.GetComponentInParent<ArrowController>() != null) continue;
            if (h.collider is CharacterController) continue;
            best = h.distance;
            point = h.point;
        }
        Vector3 dir = point - from;
        return dir.sqrMagnitude > 1e-4f ? dir.normalized : cam.transform.forward;
    }

    void Fire(Vector3 aim)
    {
        var bow = held.GetComponent<BowController>();
        string type = ArcheryTypes.Of(held.name);

        // Une flèche à copier : du même type que l'arc de préférence (pas une flèche du tir triple)
        ArrowController model = null;
        foreach (var a in FindObjectsByType<ArrowController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (a.name.Contains("tir triple") || a.transform.IsChildOf(held.transform)) continue;
            if (model == null || (ArcheryTypes.Of(a.name) == type && ArcheryTypes.Of(model.name) != type)) model = a;
        }
        if (model == null)
        {
            Say("Aucune flèche dans la scène à copier : pose une flèche (Arrow_Base) quelque part.");
            return;
        }

        var go = Instantiate(model.gameObject);
        go.name = "Flèche de test " + type;
        go.SetActive(true);
        var arrow = go.GetComponent<ArrowController>();
        if (!go.TryGetComponent(out ArrowHit hit)) hit = go.AddComponent<ArrowHit>();

        // Pleine tension : vitesse max de l'arc. Le tir compte pour le score, comme une vraie flèche.
        arrow.bow = bow;   // traînée et gravité de cet arc
        Vector3 origin = bow.arrowNock.position;
        hit.DebugLaunch(origin, aim, bow.maxSpeed);
        bow.NotifyFired(arrow, aim, bow.maxSpeed, origin);   // effets de l'arc (ex. tir triple du Bow_Monkey)
        Destroy(go, arrowLifetime);
    }

    void Say(string message)
    {
        lastMessage = message;
        messageUntil = Time.time + 3f;
        Debug.Log("[Test clavier] " + message);
    }

    void OnGUI()
    {
        if (!showHelp)
        {
            GUI.Label(new Rect(10, 10, 300, 22), "H : aide des touches");
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 380, 320), GUI.skin.box);
        GUILayout.Label("<b>Test des arcs au clavier</b>");
        GUILayout.Label("M : afficher / cacher les arcs autour de toi");
        GUILayout.Label("← → : faire tourner les arcs");
        GUILayout.Label($"Entrée : prendre l'arc en face   |   1 à {Mathf.Min(9, summoner != null ? summoner.BowCount : 0)} : prendre l'arc n°");
        GUILayout.Label("F : tirer une flèche avec l'arc (vers la souris)");
        GUILayout.Label("G : lâcher l'arc (il flotte à côté de toi)");
        GUILayout.Label("T : lancer l'arc (il disparaît)");
        GUILayout.Label("R : reprendre l'arc qui flotte à côté de toi");
        GUILayout.Label("H : cacher cette aide");
        GUILayout.Space(6);
        GUILayout.Label("Arc en main : " + (held != null ? held.Data.displayName : "aucun"));
        if (Time.time < messageUntil) GUILayout.Label(lastMessage);
        GUILayout.EndArea();
    }
}