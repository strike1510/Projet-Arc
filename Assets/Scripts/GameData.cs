using UnityEngine;

public static class GameData
{
    // niveau = numéro 1-based (cohérent avec ScoreManager.currentLevel)
    public static int GetBest(int level) => PlayerPrefs.GetInt("best_level_" + level, 0);

    public static void SetBest(int level, int score)
    {
        if (score > GetBest(level))
        {
            PlayerPrefs.SetInt("best_level_" + level, score);
            PlayerPrefs.Save();
        }
    }
}