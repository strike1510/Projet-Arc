using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place d'un coup plein d'animaux au hasard autour de cet objet (un enclos, un rocher, un arbre…).
/// Dans l'Inspector : clic droit sur le titre du composant → "Placer les animaux".
/// Les animaux deviennent de vrais objets de la scène (enfants de cet objet), avec le script Animal :
/// tu peux les déplacer, en supprimer ou en dupliquer ensuite. En jeu, chacun se balade au hasard.
///
/// En jeu : les animaux sont mélangés à des endroits au hasard au lancement, et de nouveaux apparaissent
/// au hasard dans la zone quand il en manque (après en avoir abattu). La Console écrit chaque apparition.
/// </summary>
public class AnimalHerd : MonoBehaviour
{
    [Tooltip("Prefabs d'animaux (ex. Deer_001, Horse_001, Dog_001… du pack ithappy). Un au hasard pour chaque animal.")]
    public GameObject[] animalPrefabs;

    [Tooltip("Nombre d'animaux à placer.")]
    public int count = 12;

    [Tooltip("Rayon de la zone autour de cet objet (m).")]
    public float areaRadius = 30f;

    [Tooltip("Pas d'animal à moins de cette distance du joueur au départ / du menu (m).")]
    public float keepAwayFromCenter = 6f;

    [Tooltip("Taille des animaux, au hasard entre ces deux valeurs.")]
    public Vector2 scale = new Vector2(0.9f, 1.1f);

    [Header("Réglages donnés à chaque animal")]
    [Tooltip("Rayon de balade de chaque animal autour de l'endroit où il est posé (m).")]
    public float wanderRadius = 10f;
    public Vector2 walkSpeed = new Vector2(0.8f, 1.6f);
    public float runSpeed = 5f;
    public int points = 20;

    [Header("En jeu")]
    [Tooltip("Au lancement, chaque animal posé est déplacé à un endroit au hasard de la zone.")]
    public bool shuffleOnStart = true;

    [Tooltip("Quand il y a moins de 'count' animaux vivants, un nouveau apparaît au hasard.")]
    public bool spawnOverTime = true;

    [Tooltip("Temps entre deux apparitions (s), au hasard entre ces deux valeurs.")]
    public Vector2 spawnInterval = new Vector2(5f, 10f);

    [Tooltip("Écrire chaque apparition dans la Console (pour vérifier).")]
    public bool logSpawns = true;

    readonly Dictionary<Animal, float> deadSince = new Dictionary<Animal, float>();

    void Start()
    {
        if (shuffleOnStart)
            foreach (var a in GetComponentsInChildren<Animal>())
                if (TryRandomPoint(out Vector3 p)) MoveAnimal(a, p, "déplacé");

        if (spawnOverTime) StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(spawnInterval.x, spawnInterval.y));

            int alive = 0;
            foreach (var a in GetComponentsInChildren<Animal>())
            {
                if (!a.IsDead) { alive++; deadSince.Remove(a); continue; }
                // Abattu depuis un moment : on le retire (il sera remplacé par un nouveau)
                if (!deadSince.ContainsKey(a)) deadSince[a] = Time.time;
                else if (Time.time - deadSince[a] > 8f) { deadSince.Remove(a); Destroy(a.gameObject); }
            }

            if (alive < count && animalPrefabs != null && animalPrefabs.Length > 0 && TryRandomPoint(out Vector3 p))
            {
                var prefab = animalPrefabs[Random.Range(0, animalPrefabs.Length)];
                if (prefab == null) continue;
                var go = Instantiate(prefab, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                go.transform.localScale = prefab.transform.localScale * Random.Range(scale.x, scale.y);
                var animal = go.GetComponent<Animal>();
                if (animal == null) animal = go.AddComponent<Animal>();
                Configure(animal);
                CopyAnimationSettings(animal, prefab.name);
                animal.respawn = false;   // c'est le troupeau qui en refait apparaître
                if (logSpawns) Debug.Log($"[AnimalHerd] Nouvel animal '{prefab.name}' apparu en ({p.x:0}, {p.z:0}). Vivants : {alive + 1}/{count}", go);
            }
        }
    }

    /// <summary>Reprend les réglages d'animation d'un animal du même type déjà posé (lus dans l'éditeur).</summary>
    void CopyAnimationSettings(Animal target, string prefabName)
    {
        foreach (var a in GetComponentsInChildren<Animal>(true))
        {
            if (a == target || !a.animationsDetected || !a.name.StartsWith(prefabName)) continue;
            target.speedParameter = a.speedParameter;
            target.speedParameterY = a.speedParameterY;
            target.idleValue = a.idleValue; target.walkValue = a.walkValue; target.runValue = a.runValue;
            target.idleValueY = a.idleValueY; target.walkValueY = a.walkValueY; target.runValueY = a.runValueY;
            target.animationsDetected = true;
            return;
        }
    }

    void MoveAnimal(Animal a, Vector3 p, string verb)
    {
        a.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        a.SetHome(p);
        a.respawn = false;
        if (logSpawns) Debug.Log($"[AnimalHerd] '{a.name}' {verb} au hasard en ({p.x:0}, {p.z:0}).", a);
    }

    void Configure(Animal animal)
    {
        animal.wanderRadius = wanderRadius;
        animal.walkSpeed = Random.Range(walkSpeed.x, walkSpeed.y);
        animal.runSpeed = runSpeed;
        animal.idleTime = new Vector2(Random.Range(1f, 3f), Random.Range(4f, 8f));   // chacun son rythme
        if (!animal.healthFromSize) animal.points = points;
    }

    /// <summary>Point au sol au hasard dans la zone, loin des obstacles et du centre.</summary>
    bool TryRandomPoint(out Vector3 point)
    {
        point = default;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 r = Random.insideUnitCircle * areaRadius;
            if (r.magnitude < keepAwayFromCenter) continue;
            Vector3 p = transform.position + new Vector3(r.x, 0f, r.y);
            bool found = false;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 100f, Vector3.down, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<CreatureBase>() != null) continue;
                if (!found || hit.point.y > p.y) { p.y = hit.point.y; found = true; }
            }
            if (!found) continue;
            if (Physics.CheckSphere(p + Vector3.up * 0.8f, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            point = p;
            return true;
        }
        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("Placer les animaux")]
    void PlaceAnimals()
    {
        if (animalPrefabs == null || animalPrefabs.Length == 0)
        {
            Debug.LogError("[AnimalHerd] Glisse au moins un prefab d'animal dans 'Animal Prefabs'.", this);
            return;
        }

        var placed = new List<Vector3>();
        int made = 0;
        for (int attempt = 0; attempt < count * 20 && made < count; attempt++)
        {
            Vector2 r = Random.insideUnitCircle * areaRadius;
            if (r.magnitude < keepAwayFromCenter) continue;
            Vector3 p = transform.position + new Vector3(r.x, 0f, r.y);

            // Sol sous ce point
            if (!Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                continue;
            if (hit.collider.GetComponentInParent<CreatureBase>() != null) continue;
            p = hit.point;

            // Pas sur un arbre, une maison, ni collé à un autre animal
            if (Physics.CheckSphere(p + Vector3.up * 0.8f, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            bool tooClose = false;
            foreach (var q in placed) if ((q - p).sqrMagnitude < 4f) { tooClose = true; break; }
            if (tooClose) continue;

            var prefab = animalPrefabs[Random.Range(0, animalPrefabs.Length)];
            if (prefab == null) continue;

            var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, transform);
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Placer les animaux");
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale = prefab.transform.localScale * Random.Range(scale.x, scale.y);

            var animal = go.GetComponent<Animal>();
            if (animal == null) animal = UnityEditor.Undo.AddComponent<Animal>(go);
            Configure(animal);
            animal.DetectAnimations();   // lit le Blend Tree : les pattes bougent aussi dans le casque

            placed.Add(p);
            made++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[AnimalHerd] {made} animaux placés. Ctrl+Z pour annuler, Ctrl+S pour sauvegarder.", this);
    }

    [ContextMenu("Détecter les animations de tous les animaux")]
    void DetectAll()
    {
        foreach (var a in GetComponentsInChildren<Animal>(true)) a.DetectAnimations();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    [ContextMenu("Supprimer les animaux placés")]
    void RemoveAnimals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.GetComponent<Animal>() != null) UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
#endif

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, areaRadius);
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, keepAwayFromCenter);
    }
}
