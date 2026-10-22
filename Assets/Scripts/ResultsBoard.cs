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

    [Header("Fin de manche : message")]
    [Tooltip("Gros message affiché seulement en fin de manche (\"Manche 1 terminée : 120 pts\").")]
    public TMP_Text endMessageText;
    public Color endMessageColor = new Color(1f, 0.84f, 0f);

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

    bool subscribed;

    void Start()
    {
        EnsureXRInput();
        TrySubscribe();
        if (!subscribed) Debug.LogWarning("[ResultsBoard] Pas encore de ScoreManager : le panneau attend qu'il apparaisse.");
    }

    void Update()
    {
        if (!subscribed) TrySubscribe();   // ScoreManager créé plus tard (ou scène chargée dans un autre ordre)
    }

    void TrySubscribe()
    {
        var sm = ScoreManager.Instance;
        if (sm == null) return;
        sm.OnScoreChanged += Refresh;
        subscribed = true;
        Refresh();
    }

    void OnDestroy()
    {
        if (subscribed && ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged -= Refresh;
    }

    // ---------- Boutons ----------

    public void NextRound() { if (ScoreManager.Instance != null && !ScoreManager.Instance.IsTimeUp) ScoreManager.Instance.StartNextRound(); }
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

        if (totalText != null)
        {
            string total = string.Format(totalFormat, sm.TotalScore);
            totalText.text = sm.IsRoundOver ? $"{total}  -  {PlaceText(sm.LevelIndex, sm.TotalScore)}" : total;
        }

        if (endMessageText != null)
        {
            endMessageText.gameObject.SetActive(sm.IsRoundOver);
            if (sm.IsRoundOver)
            {
                endMessageText.color = endMessageColor;
                endMessageText.text = sm.IsTimeUp
                    ? $"Temps écoulé !\n<size=70%>Total : {sm.TotalScore} pts</size>"
                    : sm.TargetReached
                    ? $"Objectif atteint !\n<size=70%>Manche {sm.RoundNumber} : {sm.RoundScore} pts</size>"
                    : $"Manche {sm.RoundNumber} terminée\n<size=70%>{sm.RoundScore} pts</size>";
            }
        }

        if (statusText != null)
        {
            if (sm.IsTimeUp)
                statusText.text = "Temps écoulé : Nouvelle partie ou Menu";
            else if (sm.IsRoundOver)
                statusText.text = sm.TargetReached ? $"Manche {sm.RoundNumber} : objectif atteint !" : $"Manche {sm.RoundNumber} terminée";
            else
                statusText.text = sm.ArrowsLeft switch
                {
                    0 => "Dernière flèche en vol...",
                    1 => "Dernière flèche !",
                    _ => $"{sm.ArrowsLeft} flèches restantes - objectif {sm.targetScore} pts"
                };
        }

        if (endButtons != null)
        {
            endButtons.SetActive(sm.IsRoundOver);
            // Temps écoulé : plus de "Manche suivante"
            foreach (var b in endButtons.GetComponentsInChildren<Button>(true))
                if (b.name.ToLower().Contains("suivante")) b.gameObject.SetActive(!sm.IsTimeUp);
        }

        RefreshRows(sm);
    }

    /// <summary>Place qu'aurait ce total dans le top 3 du niveau (sans l'enregistrer).</summary>
    static string PlaceText(int level, int total)
    {
        var entries = Leaderboard.Get(level);
        int place = 0;
        foreach (var e in entries) if (e.score >= total) place++;   // à égalité, l'ancien score reste devant
        if (place < Leaderboard.Size) return place == 0 ? "1re place !" : $"{place + 1}e place";
        return $"hors top {Leaderboard.Size} (3e : {entries[Leaderboard.Size - 1].score} pts)";
    }

    /// <summary>Nom des lignes posées dans l'éditeur sous "Lignes" (une par manche affichée).</summary>
    public const string SlotPrefix = "Manche emplacement ";

    void RefreshRows(ScoreManager sm)
    {
        if (roundsContainer == null) return;

        // Lignes déjà posées dans la scène (Projet Arc > Configurer le projet) : on les remplit et on montre celles utilisées
        var slots = new List<RoundRow>();
        foreach (Transform child in roundsContainer)
            if (child.name.StartsWith(SlotPrefix) && child.TryGetComponent(out RoundRow rr)) slots.Add(rr);

        if (slots.Count == 0)
        {
            Debug.LogWarning("[ResultsBoard] Pas de lignes posées sous 'Lignes' : lance Projet Arc > Configurer le projet.", this);
            return;
        }

        int count = Mathf.Min(slots.Count, sm.RoundScores.Count);
        int first = sm.RoundScores.Count - count;
        for (int i = 0; i < slots.Count; i++)
        {
            var row = slots[i];
            bool used = i < count;
            row.gameObject.SetActive(used);
            if (!used) continue;

            int r = first + i;
            if (row.roundLabel != null) row.roundLabel.text = string.Format(roundFormat, r + 1);
            if (row.scoreLabel != null) row.scoreLabel.text = string.Format(scoreFormat, sm.RoundScores[r]);
            FillDots(row.dotsContainer, sm.ShotsOfRound(r), sm.arrowsPerRound);
        }
    }

    void FillDots(Transform container, IReadOnlyList<ScoreManager.Shot> shots, int total)
    {
        if (container == null) return;

        // Les ronds sont posés dans la scène : on les colore, on cache ceux en trop
        int i = 0;
        foreach (Transform child in container)
        {
            if (!child.TryGetComponent(out Image dot)) continue;
            bool used = i < total;
            child.gameObject.SetActive(used);
            if (used) dot.color = i < shots.Count ? ShotColor(shots[i]) : colorEmpty;
            i++;
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
    public void BuildBoard()
    {
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null) { Debug.LogError("[ResultsBoard] Pas de Canvas sous ce panneau.", this); return; }
        var root = (RectTransform)canvas.transform;
        UnityEditor.Undo.RegisterFullObjectHierarchyUndo(gameObject, "Construire le panneau");

        playerText = EnsureText(root, "Joueur", "Prénom - Niveau 1", 44, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 300), new Vector2(900, 70));
        totalText = EnsureText(root, "Total", "Total : 0 pts", 40, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0, -250), new Vector2(900, 60));
        statusText = EnsureText(root, "Etat", "10 flèches restantes", 30, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(0, -310), new Vector2(900, 50));

        endMessageText = EnsureText(root, "MessageFin", "Manche 1 terminée\n<size=70%>120 pts</size>", 48, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0, 205), new Vector2(900, 110));
        endMessageText.color = endMessageColor;
        endMessageText.outlineWidth = 0.25f;
        endMessageText.outlineColor = new Color32(0, 0, 0, 255);
        endMessageText.transform.SetAsLastSibling();   // devant les lignes
        endMessageText.gameObject.SetActive(false);

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

    /// <summary>
    /// Pose dans la scène les lignes des manches (maxRoundsShown) et leurs ronds (un par flèche),
    /// copiées depuis "LigneModele" et "Rond". Rien n'est créé pendant le jeu.
    /// </summary>
    public void BuildSlots(int arrows)
    {
        if (roundsContainer == null || rowPrefab == null || dotPrefab == null) BuildBoard();
        if (roundsContainer == null || rowPrefab == null || dotPrefab == null) return;

        // Anciennes lignes posées : on les refait (nombre de flèches peut avoir changé)
        for (int i = roundsContainer.childCount - 1; i >= 0; i--)
        {
            var child = roundsContainer.GetChild(i);
            if (!child.name.StartsWith(SlotPrefix) && child.GetComponent<RoundRow>() == null) continue;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child.gameObject)
                && !UnityEditor.PrefabUtility.IsAddedGameObjectOverride(child.gameObject))
            {
                child.gameObject.SetActive(false);   // objet du prefab : on ne peut que le cacher
                child.name = "(ancienne ligne)";
                continue;
            }
            DestroyImmediate(child.gameObject);
        }

        for (int r = 0; r < maxRoundsShown; r++)
        {
            var row = Instantiate(rowPrefab, roundsContainer);
            row.name = SlotPrefix + (r + 1);
            if (row.roundLabel != null) row.roundLabel.text = string.Format(roundFormat, r + 1);
            if (row.scoreLabel != null) row.scoreLabel.text = string.Format(scoreFormat, 0);
            if (row.dotsContainer != null)
            {
                for (int i = row.dotsContainer.childCount - 1; i >= 0; i--) DestroyImmediate(row.dotsContainer.GetChild(i).gameObject);
                for (int i = 0; i < arrows; i++)
                {
                    var dot = Instantiate(dotPrefab, row.dotsContainer);
                    dot.name = "Rond " + (i + 1);
                    dot.color = colorEmpty;
                    dot.gameObject.SetActive(true);
                }
            }
            row.gameObject.SetActive(false);
        }
        UnityEditor.EditorUtility.SetDirty(this);
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
