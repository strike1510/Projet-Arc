using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class LevelMenuUI : MonoBehaviour
{
    [Header("Panneaux")]
    public GameObject mainPanel;       // page 1
    public GameObject levelPanel;      // page 2

    [Header("Page 1")]
    public TMP_Text scoreText;         // "score joueur / requis" au centre
    public TMP_Text bestText;          // meilleur score en bas

    [Header("Niveaux")]
    public LevelInfo[] levels;         // les 3 niveaux (index 0 = niveau 1)
    public LevelButton[] levelButtons; // les 3 cases de la page 2

    void Start()
    {
        ShowMain();
    }

    void Update()
    {
        // page 1 suit le score en direct
        if (mainPanel.activeSelf && ScoreManager.Instance != null)
        {
            var sm = ScoreManager.Instance;
            scoreText.text = sm.Score + " / " + sm.requiredScore;
            bestText.text = "Meilleur : " + GameData.GetBest(sm.currentLevel);
        }
    }

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        levelPanel.SetActive(false);
    }

    public void ShowLevels()
    {
        mainPanel.SetActive(false);
        levelPanel.SetActive(true);
        RefreshLevels();
    }

    void RefreshLevels()
    {
        for (int i = 0; i < levelButtons.Length; i++)
        {
            if (i >= levels.Length) { levelButtons[i].gameObject.SetActive(false); continue; }
            levelButtons[i].gameObject.SetActive(true);
            levelButtons[i].Setup(levels[i], IsUnlocked(i), this);
        }
    }

    bool IsUnlocked(int i)
    {
        if (i == 0) return true; // niveau 1 toujours accessible
        // débloqué si le niveau précédent a atteint son score requis
        int prevLevel = i; // index i-1 = numéro de niveau i
        return GameData.GetBest(prevLevel) >= levels[i - 1].requiredScore;
    }

    public void LoadLevel(LevelInfo info)
    {
        if (string.IsNullOrEmpty(info.sceneName)) return; // scène pas encore faite
        SceneManager.LoadScene(info.sceneName);
    }
}