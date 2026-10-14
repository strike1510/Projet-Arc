using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fait apparaître des cibles au hasard dans le champ de vision du joueur.
/// Chaque cible reste "lifetime" secondes puis disparaît. À mettre sur un objet vide "Spawner" dans le niveau.
///
/// La cible utilisée est un vrai prefab du projet (ex. "Cible", sans pieds) : on la glisse dans "Target Prefab".
/// Elle doit avoir le composant Target (comme les autres cibles).
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    [Header("Cible")]
    [Tooltip("Prefab de la cible qui apparaît (ex. Cible). Doit avoir Target.")]
    public Target targetPrefab;

    [Tooltip("Taille des cibles qui apparaissent.")]
    public float targetScale = 0.6f;

    [Tooltip("Rotation ajoutée pour que la cible soit face au joueur (mets 180 si tu vois son dos).")]
    public float faceYawOffset = 0f;

    [Tooltip("Les points de ces cibles sont multipliés (2 = deux fois plus de points que les cibles fixes).")]
    public int pointsMultiplier = 2;

    [Header("Apparition")]
    [Tooltip("Temps entre deux apparitions (s), tiré au hasard entre ces deux valeurs.")]
    public Vector2 spawnInterval = new Vector2(3f, 6f);

    [Tooltip("Durée de vie d'une cible (s).")]
    public float lifetime = 10f;

    [Tooltip("Nombre maximum de cibles apparues en même temps.")]
    public int maxAlive = 3;

    [Tooltip("Délai avant la première apparition (s).")]
    public float firstDelay = 3f;

    [Header("Où (autour de la direction du regard)")]
    [Tooltip("Distance au joueur (m), min et max.")]
    public Vector2 distance = new Vector2(5f, 10f);

    [Tooltip("Angle à gauche/droite du regard (degrés). 35 = reste bien dans le champ de vision.")]
    public float maxAngle = 35f;

    [Tooltip("Hauteur au-dessus du sol (m), min et max.")]
    public Vector2 heightAboveGround = new Vector2(1f, 3f);

    [Tooltip("Rayon libre autour de la cible (évite d'apparaître dans un arbre).")]
    public float clearRadius = 0.8f;

    [Header("Mouvement")]
    [Tooltip("Chance (0 à 1) qu'une cible apparue bouge (1 = toutes). Elles volent, donc elles montent et descendent au hasard.")]
    [Range(0f, 1f)] public float moveChance = 1f;

    readonly List<GameObject> alive = new List<GameObject>();
    Camera cam;

    void Start()
    {
        cam = FindGameViewCamera();
        if (targetPrefab == null) { Debug.LogError("[TargetSpawner] Glisse un prefab de cible dans 'Target Prefab'.", this); return; }
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            var sm = ScoreManager.Instance;
            bool roundRunning = sm == null || !sm.IsRoundOver;

            alive.RemoveAll(t => t == null);
            if (roundRunning && alive.Count < maxAlive) TrySpawn();

            yield return new WaitForSeconds(Random.Range(spawnInterval.x, spawnInterval.y));
        }
    }

    void TrySpawn()
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = FindGameViewCamera();
        if (cam == null) return;

        Vector3 eye = cam.transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-maxAngle, maxAngle), Vector3.up) * forward;
            Vector3 pos = eye + dir * Random.Range(distance.x, distance.y);

            // Hauteur : au-dessus du sol trouvé sous ce point
            float groundY = eye.y - 1.6f;
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit ground, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                groundY = ground.point.y;
            pos.y = groundY + Random.Range(heightAboveGround.x, heightAboveGround.y);

            // Pas dans un arbre ou un rocher, et visible depuis le joueur
            if (Physics.CheckSphere(pos, clearRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.Linecast(eye, pos, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

            Spawn(pos, dir);
            Debug.Log($"[TargetSpawner] Cible apparue à {Vector3.Distance(eye, pos):0.0} m de la caméra '{cam.name}'.");
            return;
        }
    }

    void Spawn(Vector3 pos, Vector3 dirFromPlayer)
    {
        Quaternion rot = Quaternion.LookRotation(dirFromPlayer) * Quaternion.Euler(0f, faceYawOffset, 0f);
        var t = Instantiate(targetPrefab, pos, rot);
        t.transform.localScale = Vector3.one * targetScale * 0.02f;
        t.name = "Cible apparue";

        if (pointsMultiplier != 1)
            for (int i = 0; i < t.ringPoints.Length; i++) t.ringPoints[i] *= pointsMultiplier;

        if (Random.value < moveChance)
        {
            // Elles volent : mouvement Auto → haut/bas, au hasard
            var mt = t.gameObject.AddComponent<MovingTarget>();
            mt.motion = MovingTarget.Motion.Auto;
            mt.aleatoire = true;
            mt.amplitude = Random.Range(0.8f, 1.5f);
            mt.speed = Random.Range(0.3f, 0.6f);
        }

        alive.Add(t.gameObject);
        StartCoroutine(Life(t.transform));
    }

    IEnumerator Life(Transform t)
    {
        Vector3 full = Vector3.one * targetScale;

        // Apparition : grossit en 0,3 s
        for (float k = 0f; k < 1f && t != null; k += Time.deltaTime / 0.3f)
        {
            t.localScale = full * Mathf.Max(0.02f, Mathf.SmoothStep(0f, 1f, k));   // jamais 0 (les colliders n'aiment pas)
            yield return null;
        }
        if (t == null) yield break;
        t.localScale = full;

        // Durée de vie (en pause quand la manche est finie : la cible reste figée)
        for (float left = Mathf.Max(0f, lifetime - 0.6f); left > 0f && t != null; )
        {
            var sm = ScoreManager.Instance;
            if (sm == null || !sm.IsRoundOver) left -= Time.deltaTime;
            yield return null;
        }
        if (t == null) yield break;

        // Disparition : rétrécit en 0,3 s
        for (float k = 0f; k < 1f && t != null; k += Time.deltaTime / 0.3f)
        {
            t.localScale = full * Mathf.Max(0.02f, 1f - Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        if (t == null) yield break;

        // Les flèches plantées dedans tombent au lieu de disparaître avec la cible
        foreach (var arrow in t.GetComponentsInChildren<ArrowHit>(true)) arrow.Unstick();

        alive.Remove(t.gameObject);
        Destroy(t.gameObject);
    }

    /// <summary>La caméra du joueur (celle affichée à l'écran), même si un pack a ajouté une autre "Main Camera".</summary>
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

    // Montre la zone d'apparition (vue de dessus) quand le Spawner est sélectionné
    void OnDrawGizmosSelected()
    {
        var c = Camera.main;
        if (c == null) return;
        Vector3 eye = c.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(c.transform.forward, Vector3.up).normalized;
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.8f);
        Vector3 l = Quaternion.AngleAxis(-maxAngle, Vector3.up) * fwd, r = Quaternion.AngleAxis(maxAngle, Vector3.up) * fwd;
        Gizmos.DrawLine(eye + l * distance.x, eye + l * distance.y);
        Gizmos.DrawLine(eye + r * distance.x, eye + r * distance.y);
        Gizmos.DrawLine(eye + l * distance.x, eye + r * distance.x);
        Gizmos.DrawLine(eye + l * distance.y, eye + r * distance.y);
    }
}
