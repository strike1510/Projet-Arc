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

    public enum Appear { Grossir, SortDuSol }

    [Header("Apparition")]
    [Tooltip("Grossir : la cible apparaît en l'air en grossissant. SortDuSol : elle sort de terre en glissant, puis y rentre.")]
    public Appear appearMode = Appear.Grossir;

    [Tooltip("SortDuSol : durée de la sortie et de la rentrée (s).")]
    public float slideTime = 0.6f;

    [Tooltip("Temps entre deux apparitions (s), tiré au hasard entre ces deux valeurs.")]
    public Vector2 spawnInterval = new Vector2(3f, 6f);

    [Tooltip("Durée de vie d'une cible (s).")]
    public float lifetime = 10f;

    [Tooltip("Nombre maximum de cibles apparues en même temps.")]
    public int maxAlive = 3;

    [Tooltip("Délai avant la première apparition (s).")]
    public float firstDelay = 3f;

    public enum Zone { DevantLeJoueur, Carre }

    [Header("Zone")]
    [Tooltip("DevantLeJoueur : dans le champ de vision. Carre : n'importe où dans un carré centré sur cet objet.")]
    public Zone zone = Zone.DevantLeJoueur;

    [Tooltip("Carre : largeur (X) et profondeur (Z) du carré, en m. Le carré suit la rotation de cet objet.")]
    public Vector2 squareSize = new Vector2(30f, 30f);

    [Tooltip("Carre : la cible doit être visible depuis le joueur (pas cachée derrière un arbre).")]
    public bool mustBeVisible = true;

    [Header("Où (mode DevantLeJoueur)")]
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
            Vector3 dir, pos;
            if (zone == Zone.Carre)
            {
                // Point au hasard dans le carré (dans le repère de cet objet)
                Vector3 local = new Vector3(Random.Range(-0.5f, 0.5f) * squareSize.x, 0f, Random.Range(-0.5f, 0.5f) * squareSize.y);
                pos = transform.position + transform.rotation * local;
                dir = Vector3.ProjectOnPlane(pos - eye, Vector3.up).normalized;   // la cible regarde le joueur
                if (dir.sqrMagnitude < 0.01f) dir = forward;
            }
            else
            {
                dir = Quaternion.AngleAxis(Random.Range(-maxAngle, maxAngle), Vector3.up) * forward;
                pos = eye + dir * Random.Range(distance.x, distance.y);
            }

            // Hauteur : au-dessus du sol trouvé sous ce point
            float groundY = eye.y - 1.6f;
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit ground, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                groundY = ground.point.y;
            bool fromGround = appearMode == Appear.SortDuSol;
            pos.y = fromGround ? groundY : groundY + Random.Range(heightAboveGround.x, heightAboveGround.y);

            // Pas dans un arbre ou un rocher, et visible depuis le joueur
            Vector3 check = fromGround ? pos + Vector3.up * 1.2f : pos;
            if (Physics.CheckSphere(check, clearRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            if ((zone == Zone.DevantLeJoueur || mustBeVisible) && Physics.Linecast(eye, check, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

            Spawn(pos, dir);
            Debug.Log($"[TargetSpawner] Cible apparue à {Vector3.Distance(eye, pos):0.0} m de la caméra '{cam.name}'.");
            return;
        }
    }

    void Spawn(Vector3 pos, Vector3 dirFromPlayer)
    {
        Quaternion rot = Quaternion.LookRotation(dirFromPlayer) * Quaternion.Euler(0f, faceYawOffset, 0f);
        var t = Instantiate(targetPrefab, pos, rot);
        t.name = "Cible apparue";

        if (appearMode == Appear.SortDuSol)
        {
            t.transform.localScale = Vector3.one * targetScale;
            if (pointsMultiplier != 1)
                for (int i = 0; i < t.ringPoints.Length; i++) t.ringPoints[i] *= pointsMultiplier;
            alive.Add(t.gameObject);
            StartCoroutine(GroundLife(t.transform, pos));
            return;
        }
        t.transform.localScale = Vector3.one * targetScale * 0.02f;

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

        // Les flèches plantées dedans tombent au lieu de disparaître avec la cible
        foreach (var arrow in t.GetComponentsInChildren<ArrowHit>(true)) arrow.Unstick();

        // Disparition : rétrécit en 0,3 s
        for (float k = 0f; k < 1f && t != null; k += Time.deltaTime / 0.3f)
        {
            t.localScale = full * Mathf.Max(0.02f, 1f - Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        if (t == null) yield break;

        alive.Remove(t.gameObject);
        Destroy(t.gameObject);
    }

    /// <summary>Cible qui sort du sol en glissant, reste, puis rentre dans le sol.</summary>
    IEnumerator GroundLife(Transform t, Vector3 groundPoint)
    {
        // Hauteur de la cible : on la place pour que son bas touche le sol une fois sortie
        float bottom = t.position.y, top = t.position.y;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            bottom = Mathf.Min(bottom, r.bounds.min.y);
            top = Mathf.Max(top, r.bounds.max.y);
        }
        Vector3 up = groundPoint + Vector3.up * (t.position.y - bottom);   // sortie
        Vector3 down = up - Vector3.up * (top - bottom + 0.2f);             // cachée sous terre
        t.position = down;

        yield return Slide(t, down, up);
        if (t == null) yield break;

        for (float left = Mathf.Max(0f, lifetime - 2f * slideTime); left > 0f && t != null; )
        {
            var sm = ScoreManager.Instance;
            if (sm == null || !sm.IsRoundOver) left -= Time.deltaTime;
            yield return null;
        }
        if (t == null) yield break;

        foreach (var arrow in t.GetComponentsInChildren<ArrowHit>(true)) arrow.Unstick();
        yield return Slide(t, up, down);
        if (t == null) yield break;

        alive.Remove(t.gameObject);
        Destroy(t.gameObject);
    }

    IEnumerator Slide(Transform t, Vector3 from, Vector3 to)
    {
        for (float k = 0f; k < 1f && t != null; k += Time.deltaTime / Mathf.Max(0.05f, slideTime))
        {
            t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        if (t != null) t.position = to;
    }

    /// <summary>La caméra du joueur (celle affichée à l'écran), même si un pack a ajouté une autre "Main Camera".</summary>
    public static Camera FindGameViewCamera()
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
        if (zone == Zone.Carre)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.9f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, (heightAboveGround.x + heightAboveGround.y) * 0.5f, 0f),
                new Vector3(squareSize.x, Mathf.Max(0.1f, heightAboveGround.y - heightAboveGround.x), squareSize.y));
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(squareSize.x, 0f, squareSize.y));
            Gizmos.matrix = Matrix4x4.identity;
            return;
        }
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
