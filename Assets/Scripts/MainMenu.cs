using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menu de l'Open World. Tout le visuel (panneau, textes, boutons) est construit à la main dans l'éditeur ;
/// ce script ne fait que le remplir et réagir aux boutons.
///
/// Branchement des boutons (On Click () dans l'Inspector de chaque bouton) :
///   Prénom            → MainMenu.OpenKeyboard()
///   Niveau 1 / 2 / 3  → MainMenu.SelectLevel(0 / 1 / 2)   (lance le niveau si le prénom est rempli)
///   Effacer           → MainMenu.Backspace()
///   Espace            → MainMenu.Space()
///   OK (du clavier)   → MainMenu.CloseKeyboard()
///   JOUER (optionnel) → MainMenu.Play()
/// Les touches lettres sont branchées toutes seules (voir "Générer le clavier").
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Niveaux (scènes à lancer — vides tant que les niveaux ne sont pas dans le projet)")]
    public string[] levelScenes = { "", "", "" };
    public string[] levelNames = { "Niveau 1", "Niveau 2", "Niveau 3" };

    [Header("Prénom")]
    [Tooltip("Texte qui affiche le prénom en cours de saisie.")]
    public TMP_Text nameText;
    [Tooltip("Écrit devant le prénom sur le parchemin.")]
    public string namePrefix = "Prénom : ";
    [Tooltip("Affiché tant que le prénom est vide.")]
    public string namePlaceholder = "Prénom : touche pour écrire";
    public Color nameTextColor = Color.white;
    public Color placeholderTextColor = new Color(1f, 1f, 1f, 0.7f);
    public int maxNameLength = 12;

    [Header("Clavier virtuel")]
    [Tooltip("Panneau du clavier : caché au départ, ouvert par le bouton Prénom, fermé par OK.")]
    public GameObject keyboardPanel;
    [Tooltip("Objet qui contient les touches (avec un Grid Layout Group de préférence).")]
    public Transform keysContainer;
    [Tooltip("Modèle d'une touche : un Button avec un texte TMP en enfant.")]
    public Button keyPrefab;
    [Tooltip("Lettres du clavier, dans l'ordre.")]
    public string keys = "AZERTYUIOPQSDFGHJKLMWXCVBN";

    [Header("Choix du niveau")]
    [Tooltip("Les 3 boutons de niveau, dans l'ordre.")]
    public Button[] levelButtons = new Button[3];
    public Color levelNormalColor = Color.white;
    public Color levelSelectedColor = new Color(0.55f, 0.95f, 0.45f);
    [Tooltip("Coché : cliquer sur un niveau le lance directement (si le prénom est rempli).")]
    public bool playOnLevelClick = true;

    [Tooltip("Le texte titre de chaque bouton de niveau, dans l'ordre : y est écrit le nom du niveau (Level Names).")]
    public TMP_Text[] levelTitleTexts = new TMP_Text[3];

    [Tooltip("Un texte par bouton de niveau, dans l'ordre : y est écrit le meilleur score du niveau.")]
    public TMP_Text[] levelRecordTexts = new TMP_Text[3];
    [Tooltip("{0} = prénom, {1} = score")]
    public string recordFormat = "Record : {0} - {1}";
    public string noRecordText = "Pas encore de record";

    [Header("Top 3 détaillé (optionnel, peut rester vide)")]
    [Tooltip("Un texte par niveau, dans l'ordre (Niveau 1, 2, 3). Rempli avec le top 3.")]
    public TMP_Text[] leaderboardTexts = new TMP_Text[3];
    [Tooltip("{0} = place, {1} = prénom, {2} = score")]
    public string leaderboardLine = "{0}. {1}  {2}";
    public string leaderboardEmptyLine = "{0}. ---";

    [Header("Message (erreurs, infos)")]
    public TMP_Text messageText;

    string playerName = "";
    int selectedLevel = -1;

    void Start()
    {
        GameSettings.MenuScene = SceneManager.GetActiveScene().name;
        // Ordre des niveaux pour le bouton "Niveau suivant" (case vide = scène "NiveauN" par défaut)
        int count = Mathf.Max(levelScenes.Length, levelNames.Length);
        GameSettings.LevelScenes = new string[count];
        GameSettings.LevelNames = new string[count];
        for (int i = 0; i < count; i++)
        {
            string sc = i < levelScenes.Length ? levelScenes[i] : "";
            GameSettings.LevelScenes[i] = string.IsNullOrWhiteSpace(sc) ? $"Niveau{i + 1}" : sc;
            GameSettings.LevelNames[i] = i < levelNames.Length && !string.IsNullOrWhiteSpace(levelNames[i]) ? levelNames[i] : $"Niveau {i + 1}";
        }
        EnsureXRInput();
        WireKeyboard();

        if (GameSettings.PlayerName != "Joueur") playerName = GameSettings.PlayerName;
        HighlightLevel(GameSettings.LevelIndex);
        if (keyboardPanel != null) keyboardPanel.SetActive(false);
        RefreshName();
        RefreshLeaderboards();
        SetMessage("");
    }

    void OnEnable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnPhysicalKey;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnPhysicalKey;
    }

    // ---------- Actions branchées sur les boutons ----------

    public void SelectLevel(int index)
    {
        Debug.Log($"[MainMenu] Clic sur le niveau {index + 1} (prénom : '{playerName}', scène : '{(index >= 0 && index < levelScenes.Length ? levelScenes[index] : "")}').");
        if (!LevelProgress.IsUnlocked(index))
        {
            string prev = index - 1 < levelNames.Length && index >= 1 ? levelNames[index - 1] : $"Niveau {index}";
            SetMessage($"Niveau verrouillé : termine d'abord {prev} (1 min 30) pour l'ouvrir.");
            return;
        }
        HighlightLevel(index);
        if (selectedLevel < 0) return;

        if (!playOnLevelClick) { SetMessage(""); return; }

        if (playerName.Trim().Length == 0)
        {
            SetMessage("Entre ton prénom d'abord !");
            OpenKeyboard();
            return;
        }
        Play();
    }

    void HighlightLevel(int index)
    {
        selectedLevel = index >= 0 && index < levelButtons.Length ? index : -1;

        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (levelButtons[i] == null) continue;
            var img = levelButtons[i].targetGraphic;
            if (img != null) img.color = i == selectedLevel ? levelSelectedColor : levelNormalColor;
        }
    }

    public void OpenKeyboard()
    {
        if (keyboardPanel != null) keyboardPanel.SetActive(true);
    }

    public void CloseKeyboard()
    {
        if (keyboardPanel != null) keyboardPanel.SetActive(false);
        if (playerName.Trim().Length > 0) SetMessage("");
    }

    public void TypeLetter(string letter)
    {
        if (string.IsNullOrEmpty(letter) || playerName.Length >= maxNameLength) return;
        playerName += letter;
        RefreshName();
        SetMessage("");
    }

    public void Space()
    {
        if (playerName.Length > 0 && !playerName.EndsWith(" ")) TypeLetter(" ");
    }

    public void Backspace()
    {
        if (playerName.Length == 0) return;
        playerName = playerName.Substring(0, playerName.Length - 1);
        RefreshName();
    }

    public void Play()
    {
        string name = playerName.Trim();
        if (name.Length == 0) { SetMessage("Entre ton prénom d'abord !"); return; }
        if (selectedLevel < 0) { SetMessage("Choisis un niveau !"); return; }
        if (!LevelProgress.IsUnlocked(selectedLevel)) { SetMessage("Ce niveau est encore verrouillé."); return; }

        string levelName = selectedLevel < levelNames.Length ? levelNames[selectedLevel] : $"Niveau {selectedLevel + 1}";
        string scene = selectedLevel < levelScenes.Length ? levelScenes[selectedLevel] : "";

        if (string.IsNullOrWhiteSpace(scene))
        {
            SetMessage($"{levelName} n'est pas encore disponible.");
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            SetMessage($"Scène introuvable : {scene}");
            Debug.LogError($"[MainMenu] La scène '{scene}' n'est pas dans File > Build Profiles > Scene List.");
            return;
        }

        GameSettings.PlayerName = name;
        GameSettings.LevelIndex = selectedLevel;
        GameSettings.LevelName = levelName;
        SceneManager.LoadScene(scene);
    }

    // ---------- Affichage ----------

    void RefreshName()
    {
        if (nameText == null) return;
        bool empty = playerName.Length == 0;
        nameText.text = empty ? namePlaceholder : namePrefix + playerName;
        nameText.color = empty ? placeholderTextColor : nameTextColor;
    }

    public void RefreshLeaderboards()
    {
        // Niveaux verrouillés : bouton grisé tant que le niveau d'avant n'a pas été fini
        for (int i = 0; i < levelButtons.Length; i++)
            if (levelButtons[i] != null) levelButtons[i].interactable = LevelProgress.IsUnlocked(i);

        // Nom de chaque niveau sur son bouton
        for (int level = 0; level < levelTitleTexts.Length; level++)
            if (levelTitleTexts[level] != null && level < levelNames.Length)
                levelTitleTexts[level].text = levelNames[level];

        // Meilleur score affiché sur chaque bouton de niveau
        for (int level = 0; level < levelRecordTexts.Length; level++)
        {
            if (levelRecordTexts[level] == null) continue;
            var top = Leaderboard.Get(level);
            string record = !LevelProgress.IsUnlocked(level)
                ? $"Verrouillé : finis le niveau {level}"
                : top.Count > 0 ? string.Format(recordFormat, top[0].name, top[0].score) : noRecordText;

            // Le nom du niveau d'abord, le record en dessous — dans le même texte si le bouton n'en a qu'un
            var title = level < levelTitleTexts.Length ? levelTitleTexts[level] : null;
            bool sameText = title == null || title == levelRecordTexts[level];
            string levelName = level < levelNames.Length ? levelNames[level] : $"Niveau {level + 1}";
            levelRecordTexts[level].text = sameText ? $"<b>{levelName}</b>\n<size=75%>{record}</size>" : record;
        }

        // Top 3 détaillé (si des textes sont renseignés)
        for (int level = 0; level < leaderboardTexts.Length; level++)
        {
            if (leaderboardTexts[level] == null) continue;

            var top = Leaderboard.Get(level);
            var lines = new List<string>();
            for (int i = 0; i < Leaderboard.Size; i++)
                lines.Add(i < top.Count
                    ? string.Format(leaderboardLine, i + 1, top[i].name, top[i].score)
                    : string.Format(leaderboardEmptyLine, i + 1));

            leaderboardTexts[level].text = string.Join("\n", lines);
        }
    }

    void SetMessage(string msg)
    {
        if (messageText != null) messageText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log($"[MainMenu] {msg}");
    }

    // ---------- Clavier ----------

    /// <summary>
    /// Branche chaque touche du conteneur sur TypeLetter (d'après le texte de la touche).
    /// S'il n'y a aucune touche, les crée à partir du modèle.
    /// </summary>
    void WireKeyboard()
    {
        if (keysContainer == null) return;
        if (keysContainer.GetComponentsInChildren<Button>(true).Length == 0) CreateKeys();

        foreach (var button in keysContainer.GetComponentsInChildren<Button>(true))
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null || label.text.Trim().Length != 1) continue;   // ignore Effacer / Espace
            string letter = label.text.Trim().ToUpper();
            button.onClick.AddListener(() => TypeLetter(letter));
        }
    }

    void CreateKeys()
    {
        if (keyPrefab == null || keysContainer == null) return;
        foreach (char c in keys)
        {
            var key = Instantiate(keyPrefab, keysContainer);
            key.name = "Touche " + c;
            var label = key.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = c.ToString();
            key.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Clic droit sur le composant > "Générer le clavier" : crée les touches dans la scène, dans l'éditeur.
    /// Elles deviennent de vrais objets que tu peux déplacer, recolorer ou supprimer.
    /// </summary>
    [ContextMenu("Générer le clavier")]
    void GenerateKeyboardInEditor()
    {
        if (keyPrefab == null || keysContainer == null)
        {
            Debug.LogError("[MainMenu] Renseigne d'abord 'Keys Container' et 'Key Prefab'.");
            return;
        }
        for (int i = keysContainer.childCount - 1; i >= 0; i--)
        {
            var child = keysContainer.GetChild(i);
            if (child != keyPrefab.transform) DestroyImmediate(child.gameObject);
        }
        CreateKeys();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(keysContainer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    [ContextMenu("Réinitialiser la progression (seul le Niveau 1 ouvert)")]
    void ResetProgress()
    {
        LevelProgress.Reset();
        RefreshLeaderboards();
        Debug.Log("[MainMenu] Progression remise à zéro.");
    }

    [ContextMenu("Effacer tous les classements")]
    void ClearLeaderboards()
    {
        for (int i = 0; i < levelScenes.Length; i++) Leaderboard.Clear(i);
        RefreshLeaderboards();
        Debug.Log("[MainMenu] Classements effacés.");
    }

    void OnPhysicalKey(char c)
    {
        if (c == '\b') Backspace();
        else if (c == ' ') Space();
        else if (char.IsLetterOrDigit(c) || c == '-') TypeLetter(char.ToUpper(c).ToString());
    }

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
