using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// OUTIL DE TEST SANS CASQUE (à ne pas laisser dans la scène finale).
/// Clic gauche ou Espace dans la vue Game : tire la flèche là où pointe la souris.
/// Le calcul des points passe par le vrai chemin (ArrowHit → Target → ScoreManager).
/// </summary>
public class TestShooter : MonoBehaviour
{
    [Tooltip("La flèche de la scène (l'objet qui a ArrowHit).")]
    public ArrowHit arrow;

    [Tooltip("Vitesse du tir (l'arc tire entre 5 et 30 m/s).")]
    public float speed = 30f;

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

        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (!click && !space) return;

        Vector2 screenPos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width / 2f, Screen.height / 2f);

        Ray ray = cam.ScreenPointToRay(screenPos);
        arrow.DebugLaunch(ray.origin + ray.direction * 0.5f, ray.direction, speed);
    }
}
