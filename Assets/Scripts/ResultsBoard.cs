using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Panneau des résultats d'un niveau, construit à la main dans l'éditeur sur le panneau en bois.
/// Affiche en direct : prénom + niveau, une ligne par manche avec un rond coloré par flèche, le total,
/// l'état de la manche, et les boutons de fin de manche.
///
/// Branchement des boutons (On Click ()) :
///   Manche suivante → ResultsBoard.NextRound()
///   Nouvelle partie → ResultsBoard.NewGame()
///   Menu            → ResultsBoard.BackToMenu()
/// </summary>
public class ResultsBoard : MonoBehaviour
{
    [Header("Textes")]
    [Tooltip("En haut du panneau : prénom et niveau.")]
    public TMP_Text playerText;
    [Tooltip("{0} = prénom, {1} = niveau")]
    public string playerFormat = "{0} - {1}";

    [Tooltip("Total de la partie (toutes les manches).")]
    public TMP_Text totalText;
    public string totalFormat = "Total : {0} pts";

    [Tooltip("Ligne d'état : flèches restantes, fin de manche...")]
    public TMP_Text statusText;

    [Header("Lignes des manches")]
    [Tooltip("Objet qui contient les lignes (avec un Vertical Layout Group).")]
    public Transform roundsContainer;
    [Tooltip("Prefab d'une ligne (avec le composant RoundRow).")]
    public RoundRow rowPrefab;
    [Tooltip("Nombre maximum de manches affichées (les plus récentes).")]
    public int maxRoundsShown = 4;
    public string roundFormat = "Manche {0}";
    public string scoreFormat = "{0} pts";

    [Header("Ronds (un par flèche)")]
    [Tooltip("Prefab d'un rond : une Image (ex. sprite \"Knob\").")]
    public Image dotPrefab;
    public Color colorBullseye = new Color(1f, 0.84f, 0f);       // or : centre (10)
    public Color colorEight = new Color(1f, 0.35f, 0.25f);       // rouge : 8
    public Color colorFourSix = new Color(0.45f, 0.85f, 1f);     // bleu : 4 ou 6
    public Color colorTwo = Color.white;                         // blanc : 2
    public Color colorMiss = new Color(0.35f, 0.32f, 0.3f);      // gris foncé : ratée
    public Color colorEmpty = new Color(1f, 1f, 1f, 0.25f);      // pas encore tirée

    [Header("Fin de manche")]
    [Tooltip("Groupe des boutons (Manche suivante / Nouvelle partie / Menu), affiché seulement en fin de manche.")]
    public GameObject endButtons;

    void Start()
    {
        EnsureXRInput();

        var sm = ScoreManager.Instance;
        if (sm == null)
        {
            Debug.LogError("[ResultsBoard] Pas de ScoreManager dans la scène.");
            return;
        }
        sm.OnScoreChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged -= Refresh;
    }

    // ---------- Boutons ----------

    public void NextRound() => ScoreManager.Instance?.StartNextRound();
    public void NewGame() => ScoreManager.Instance?.ResetGame();
    public void BackToMenu() => ScoreManager.Instance?.BackToMenu();

    // ---------- Affichage ----------

    public void Refresh()
    {
        var sm = ScoreManager.Instance;
        if (sm == null) return;

        if (playerText != null)
        {
            string level = string.IsNullOrEmpty(GameSettings.LevelName) ? $"Niveau {sm.LevelIndex + 1}" : GameSettings.LevelName;
            playerText.text = string.Format(playerFormat, sm.PlayerName, level);
        }

        if (totalText != null) totalText.text = string.Format(totalFormat, sm.TotalScore);

        if (statusText != null)
        {
            if (sm.IsRoundOver)
                statusText.text = sm.TargetReached ? $"Manche {sm.RoundNumber} : objectif atteint !" : $"Manche {sm.RoundNumber} terminée";
            else
                statusText.text = sm.ArrowsLeft switch
                {
                    0 => "Dernière flèche en vol...",
                    1 => "Dernière flèche !",
                    _ => $"{sm.ArrowsLeft} flèches restantes - objectif {sm.targetScore} pts"
                };
        }

        if (endButtons != null) endButtons.SetActive(sm.IsRoundOver);

        RefreshRows(sm);
    }

    void RefreshRows(ScoreManager sm)
    {
        if (roundsContainer == null || rowPrefab == null) return;

        // On efface les lignes générées (pas celles posées à la main pour la mise en page)
        for (int i = roundsContainer.childCount - 1; i >= 0; i--)
        {
            var child = roundsContainer.GetChild(i);
            if (child.GetComponent<RoundRow>() != null) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }

        int first = Mathf.Max(0, sm.RoundScores.Count - maxRoundsShown);
        for (int r = first; r < sm.RoundScores.Count; r++)
        {
            var row = Instantiate(rowPrefab, roundsContainer);
            row.gameObject.SetActive(true);
            row.name = $"Manche {r + 1}";

            if (row.roundLabel != null) row.roundLabel.text = string.Format(roundFormat, r + 1);
            if (row.scoreLabel != null) row.scoreLabel.text = string.Format(scoreFormat, sm.RoundScores[r]);

            FillDots(row.dotsContainer, sm.ShotsOfRound(r), sm.arrowsPerRound);
        }
    }

    void FillDots(Transform container, IReadOnlyList<ScoreManager.Shot> shots, int total)
    {
        if (container == null || dotPrefab == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            var old = container.GetChild(i).gameObject;
            old.SetActive(false);   // sort tout de suite de la mise en page
            Destroy(old);
        }

        for (int i = 0; i < total; i++)
        {
            var dot = Instantiate(dotPrefab, container);
            dot.gameObject.SetActive(true);
            dot.color = i < shots.Count ? ShotColor(shots[i]) : colorEmpty;
        }
    }

    Color ShotColor(ScoreManager.Shot s)
    {
        if (s.miss) return colorMiss;
        if (s.bullseye) return colorBullseye;
        if (s.points >= 8) return colorEight;
        if (s.points >= 4) return colorFourSix;
        return s.points > 0 ? colorTwo : colorMiss;
    }

    // ---------- Construction automatique dans l'éditeur ----------

#if UNITY_EDITOR
    /// <summary>
    /// Clic droit sur le titre du composant → "Construire / réparer le panneau".
    /// Crée ou répare les vrais objets sous le Canvas (Joueur, Total, Etat, Lignes, Rond, LigneModele)
    /// et remplit tous les champs. Les objets restent modifiables à la main ensuite.
    /// </summary>
    [ContextMenu("Construire / réparer le panneau")]
    void BuildBoard()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) { Debug.LogError("[ResultsBoard] Pas de Canvas sous ce panneau.", this); return; }
        var root = (RectTransform)canvas.transform;
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Construire le panneau");

        playerText = EnsureText(root, "Joueur", "Prénom - Niveau 1", 44, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 300), new Vector2(900, 70));
        totalText = EnsureText(root, "Total", "Total : 0 pts", 40, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0, -250), new Vector2(900, 60));
        statusText = EnsureText(root, "Etat", "10 flèches restantes", 30, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0, -310), new Vector2(900, 50));

        // Conteneur des lignes de manches
        var lignes = FindOrCreate(root, "Lignes", new Vector2(0, 30), new Vector2(900, 300));
        var vlg = GetOrAdd<VerticalLayoutGroup>(lignes.gameObject);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true; vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
        roundsContainer = lignes;

        // Rond modèle (caché)
        var rond = FindOrCreate(root, "Rond", Vector2.zero, new Vector2(36, 36));
        if (rond.parent == lignes) rond.SetParent(root, false);
        rond.sizeDelta = new Vector2(36, 36);
        if (!rond.TryGetComponent(out Image img))
        {
            foreach (var g in rond.GetComponents<Graphic>()) DestroyImmediate(g);
            img = rond.gameObject.AddComponent<Image>();
        }
        if (img.sprite == null) img.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.raycastTarget = false;
        rond.gameObject.SetActive(false);
        dotPrefab = img;

        // Ligne modèle (cachée, hors de Lignes sinon elle serait effacée)
        rowPrefab = BuildRowTemplate(root);

        // Boutons de fin de manche
        var boutons = FindDeep(root, "BoutonsFin");
        if (boutons != null) endButtons = boutons.gameObject;

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log("[ResultsBoard] Panneau construit et champs remplis. Pense à sauvegarder la scène (Ctrl+S).", this);
    }

    RoundRow BuildRowTemplate(RectTransform root)
    {
        var row = FindOrCreate(root, "LigneModele", new Vector2(0, -400), new Vector2(900, 60));
        if (row.parent != root) row.SetParent(root, false);
        row.sizeDelta = new Vector2(900, 60);

        var hlg = GetOrAdd<HorizontalLayoutGroup>(row.gameObject);
        hlg.spacing = 20;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

        var manche = EnsureText(row, "TexteManche", "Manche 1", 36, FontStyles.Bold, TextAlignmentOptions.Left, Vector2.zero, new Vector2(200, 50));
        SetLayout(manche.gameObject, 200, -1);

        var ronds = FindOrCreate(row, "Ronds", Vector2.zero, new Vector2(500, 40));
        SetLayout(ronds.gameObject, 500, 40);
        var dotsLayout = GetOrAdd<HorizontalLayoutGroup>(ronds.gameObject);
        dotsLayout.spacing = 8;
        dotsLayout.childAlignment = TextAnchor.MiddleLeft;
        dotsLayout.childControlWidth = false; dotsLayout.childControlHeight = false;
        dotsLayout.childForceExpandWidth = false; dotsLayout.childForceExpandHeight = false;

        var score = EnsureText(row, "TexteScore", "0 pts", 36, FontStyles.Bold, TextAlignmentOptions.Right, Vector2.zero, new Vector2(150, 50));
        SetLayout(score.gameObject, 150, -1);

        // Ordre : Manche | ronds | score
        manche.transform.SetSiblingIndex(0);
        ronds.SetSiblingIndex(1);
        score.transform.SetSiblingIndex(2);

        var rr = GetOrAdd<RoundRow>(row.gameObject);
        rr.roundLabel = manche;
        rr.scoreLabel = score;
        rr.dotsContainer = ronds;
        row.gameObject.SetActive(false);
        return rr;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
        => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    static void SetLayout(GameObject go, float width, float height)
    {
        var le = GetOrAdd<LayoutElement>(go);
        le.preferredWidth = width;
        le.preferredHeight = height;
    }

    /// <summary>Renvoie un texte TextMeshPro pour l'objet "name" (le crée ou le convertit si besoin).</summary>
    static TMP_Text EnsureText(RectTransform parent, string name, string sample, float size, FontStyles style,
                               TextAlignmentOptions align, Vector2 pos, Vector2 dim)
    {
        var rt = FindOrCreate(parent, name, pos, dim);
        rt.TryGetComponent(out TMP_Text tmp);

        if (tmp == null)
        {
            // Un ancien "Text" (Legacy) : on le remplace
            if (rt.TryGetComponent(out Text legacy)) { if (string.IsNullOrEmpty(sample)) sample = legacy.text; DestroyImmediate(legacy); }

            if (!rt.TryGetComponent(out Graphic _))
                tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            else
            {
                // L'objet a déjà une Image (fond) : le texte va dans un enfant qui le remplit
                var child = FindOrCreate(rt, "Texte", Vector2.zero, Vector2.zero);
                child.anchorMin = Vector2.zero; child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero; child.offsetMax = Vector2.zero;
                tmp = (child.TryGetComponent(out TMP_Text ct) ? ct : child.gameObject.AddComponent<TextMeshProUGUI>());
            }
        }

        tmp.text = sample;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static RectTransform FindOrCreate(RectTransform parent, string name, Vector2 pos, Vector2 dim)
    {
        var found = FindDeep(parent, name);
        if (found != null)
        {
            var frt = found as RectTransform;
            if (frt != null) return frt;
        }
        var go = new GameObject(name, typeof(RectTransform));
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Créer " + name);
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchoredPosition = pos;
        rt.sizeDelta = dim;
        return rt;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var deeper = FindDeep(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }
#endif

    /// <summary>Rend les boutons cliquables avec les manettes VR et à la souris.</summary>
    void EnsureXRInput()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        if (es.GetComponent<XRUIInputModule>() == null)
        {
            foreach (var m in es.GetComponents<BaseInputModule>()) DestroyImmediate(m);
            es.gameObject.AddComponent<XRUIInputModule>();
        }

        foreach (var canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.renderMode != RenderMode.WorldSpace) continue;
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
            if (!canvas.TryGetComponent(out TrackedDeviceGraphicRaycaster _)) canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}
