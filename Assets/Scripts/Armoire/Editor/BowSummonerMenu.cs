using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu "Tir à l'arc" dans la barre du haut d'Unity :
/// ajoute le sélecteur d'arcs (BowSummoner) dans la scène ouverte, rempli avec tous les presets du projet.
/// </summary>
public static class BowSummonerMenu
{
    [MenuItem("Tir à l'arc/Ajouter le sélecteur d'arcs au joueur")]
    static void AddSummoner()
    {
        var existingSummoner = Object.FindAnyObjectByType<BowSummoner>();
        if (existingSummoner != null)
        {
            Selection.activeGameObject = existingSummoner.gameObject;
            EditorUtility.DisplayDialog("Sélecteur d'arcs", "Il y a déjà un sélecteur d'arcs dans cette scène (sélectionné).", "OK");
            return;
        }

        var presets = AssetDatabase.FindAssets("t:BowData")
            .Select(g => AssetDatabase.LoadAssetAtPath<BowData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null && d.prefab != null)
            .OrderBy(d => d.name)
            .ToList();

        if (presets.Count == 0)
        {
            EditorUtility.DisplayDialog("Sélecteur d'arcs",
                "Aucun preset d'arc trouvé (ou aucun n'a de prefab).\nCrée-les avec Create > Tir à l'arc > Données d'arc.", "OK");
            return;
        }

        var go = new GameObject("Sélecteur d'arcs");
        Undo.RegisterCreatedObjectUndo(go, "Ajouter le sélecteur d'arcs");
        var origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin != null) Undo.SetTransformParent(go.transform, origin.transform, "Ajouter le sélecteur d'arcs");
        go.transform.localPosition = Vector3.zero;

        var summoner = go.AddComponent<BowSummoner>();
        summoner.bows.AddRange(presets);

        // Les arcs déjà posés dans la scène feraient doublon avec ceux du sélecteur
        var existing = Object.FindObjectsByType<BowController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (existing.Length > 0 && EditorUtility.DisplayDialog("Arcs déjà dans la scène",
                $"{existing.Length} arc(s) sont déjà posés dans cette scène.\n" +
                "Le sélecteur fait apparaître ses propres arcs. Désactiver les arcs existants ?",
                "Désactiver", "Les garder"))
        {
            foreach (var b in existing)
            {
                Undo.RecordObject(b.gameObject, "Désactiver les arcs");
                b.gameObject.SetActive(false);
            }
        }

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"[Sélecteur d'arcs] Ajouté avec {presets.Count} arc(s) : {string.Join(", ", presets.Select(p => p.displayName))}. " +
                  "En jeu : bouton X (manette gauche) ou touche M pour ouvrir, joystick droit ou flèches pour tourner.", go);
    }
}
