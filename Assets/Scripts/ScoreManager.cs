using System;
using UnityEngine;

/// <summary>
/// Compte les points de la partie en cours.
/// À mettre sur un GameObject vide "ScoreManager" dans la scène de jeu (un seul).
/// Les autres scripts y accèdent via ScoreManager.Instance.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public string PlayerName => GameSettings.PlayerName;
    public int Score { get; private set; }
    public int ArrowsShot { get; private set; }
    public int Hits { get; private set; }
    public int Bullseyes { get; private set; }

    /// <summary>Précision en % (flèches qui ont touché une cible / flèches tirées).</summary>
    public float Accuracy => ArrowsShot == 0 ? 0f : 100f * Hits / ArrowsShot;

    /// <summary>Appelé à chaque changement (pour mettre à jour l'affichage).</summary>
    public event Action OnScoreChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ScoreManager] Il y a deux ScoreManager dans la scène, le second est supprimé.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterShot()
    {
        ArrowsShot++;
        OnScoreChanged?.Invoke();
    }

    public void RegisterHit(int points, bool bullseye)
    {
        Hits++;
        Score += points;
        if (bullseye) Bullseyes++;
        Debug.Log($"[Score] {PlayerName} : +{points} → {Score} pts ({Hits}/{ArrowsShot} touchées)");
        OnScoreChanged?.Invoke();
    }

    public void RegisterMiss()
    {
        Debug.Log($"[Score] Raté → {Score} pts ({Hits}/{ArrowsShot} touchées)");
        OnScoreChanged?.Invoke();
    }

    public void ResetScore()
    {
        Score = ArrowsShot = Hits = Bullseyes = 0;
        OnScoreChanged?.Invoke();
    }
}
