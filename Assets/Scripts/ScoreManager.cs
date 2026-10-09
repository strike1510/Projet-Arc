using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int currentLevel = 1;      // numéro du niveau de cette scène
    public int requiredScore = 100;   // score requis pour débloquer le suivant
    public TMP_Text scoreText;        // HUD en haut à gauche

    public int Score { get; private set; }

    void Awake()
    {
        Instance = this;
        UpdateUI();
    }

    public void AddPoints(int points)
    {
        Score += points;
        GameData.SetBest(currentLevel, Score);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "Score : " + Score;
    }
}