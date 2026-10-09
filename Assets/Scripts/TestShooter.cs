using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// OUTIL DE TEST SANS CASQUE (à ne pas laisser dans la scène finale).
/// Clic gauche ou Espace dans la vue Game : tire la flèche là où pointe la souris.
/// N : manche suivante quand la manche est finie.
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

    Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (arrow == null) arrow = FindAnyObjectByType<ArrowHit>();
        if (arrow == null) Debug.LogError("[TestShooter] Aucune flèche avec ArrowHit dans la scène.");
        else Debug.Log("[TestShooter] Clic gauche ou Espace dans la vue Game pour tirer.");
    }

    void Update()
    {
        if (arrow == null || cam == null) return;

        var sm = ScoreManager.Instance;

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

    bool TryGetAimPoint(Ray ray, out Vector3 point)
    {
        point = default;
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(ray, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(arrow.transform)) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        return best < float.MaxValue;
    }
}
