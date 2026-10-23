using UnityEngine;

/// <summary>
/// Ajoute des colliders à tout le décor rangé sous cet objet (arbres, buissons, rochers, maisons, torii…),
/// pour que les animaux, les ninjas, les flèches et le joueur ne passent plus au travers.
/// Dans l'Inspector : clic droit sur le titre du composant → "Ajouter les colliders au décor".
///   - Arbres (nom avec tree, pine, sakura, birch…) : une colonne autour du tronc seulement (on peut passer sous les branches).
///   - Gros objets (maisons, torii, ponts… plus grands que 'bigObjectSize') : collider qui épouse la forme exacte.
///   - Le reste (buissons, rochers, lanternes, champignons…) : une boîte à leur taille.
/// Les objets qui ont déjà un collider ne sont pas touchés. Annulable avec Ctrl+Z.
/// </summary>
public class DecorColliders : MonoBehaviour
{
    [Tooltip("Mots dans le nom qui veulent dire « arbre » (collider sur le tronc seulement).")]
    public string[] treeWords = { "tree", "pine", "sakura", "birch", "oak", "palm", "fir", "spruce", "maple", "bamboo" };

    [Tooltip("Au-delà de cette taille (m), collider exact (maisons, torii, ponts…).")]
    public float bigObjectSize = 4f;

    [Tooltip("Épaisseur du tronc par rapport à la largeur de l'arbre.")]
    [Range(0.05f, 0.5f)] public float trunkRatio = 0.12f;

    [Tooltip("Ignorer les objets plus petits que ça (fleurs, herbes, cailloux) (m).")]
    public float ignoreSmallerThan = 0.4f;

#if UNITY_EDITOR
    [ContextMenu("Ajouter les colliders au décor")]
    void AddColliders()
    {
        int trees = 0, big = 0, boxes = 0, skipped = 0;

        // On traite chaque "objet de décor" : les enfants directs qui ont un rendu
        foreach (Transform item in GetComponentsInChildren<Transform>(true))
        {
            if (item == transform) continue;
            if (item.GetComponent<CreatureBase>() != null || item.GetComponentInParent<CreatureBase>() != null) continue;
            if (item.GetComponent<Renderer>() == null && item.GetComponent<LODGroup>() == null) continue;
            if (item.parent != transform && item.parent.GetComponent<LODGroup>() != null) continue;   // un niveau de LOD : on traite l'objet parent
            if (item.GetComponentInChildren<Collider>() != null) { skipped++; continue; }

            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) continue;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size < ignoreSmallerThan) continue;

            string n = item.name.ToLowerInvariant();
            bool isTree = false;
            foreach (var w in treeWords) if (n.Contains(w)) { isTree = true; break; }

            if (isTree)
            {
                var cap = UnityEditor.Undo.AddComponent<CapsuleCollider>(item.gameObject);
                Vector3 s = item.lossyScale;
                float sx = Mathf.Max(0.0001f, Mathf.Abs(s.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(s.y));
                float width = Mathf.Max(b.size.x, b.size.z);
                float trunkHeight = b.size.y * 0.6f;                       // le bas de l'arbre
                Vector3 bottom = new Vector3(b.center.x, b.min.y, b.center.z);
                cap.center = item.InverseTransformPoint(bottom + Vector3.up * trunkHeight * 0.5f);
                cap.height = trunkHeight / sy;
                cap.radius = Mathf.Max(0.15f, width * trunkRatio) / sx;
                cap.direction = 1;
                trees++;
            }
            else if (size >= bigObjectSize && item.GetComponent<MeshFilter>() != null)
            {
                UnityEditor.Undo.AddComponent<MeshCollider>(item.gameObject);
                big++;
            }
            else
            {
                var box = UnityEditor.Undo.AddComponent<BoxCollider>(item.gameObject);
                Vector3 s = item.lossyScale;
                box.center = item.InverseTransformPoint(b.center);
                box.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                                       b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                                       b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
                boxes++;
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[DecorColliders] Ajoutés : {trees} troncs d'arbre, {big} colliders exacts, {boxes} boîtes. {skipped} objets avaient déjà un collider. Ctrl+Z pour annuler.", this);
    }
#endif
}
