using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Compte les points, manche par manche.
/// Une manche se termine quand on atteint 'targetScore' points OU quand on n'a plus de flèches.
/// À mettre sur un GameObject vide "ScoreManager" dans la scène de jeu (un seul).
/// Les autres scripts y accèdent via ScoreManager.Instance.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Règles d'une manche")]
    [Tooltip("La manche s'arrête dès qu'on atteint ce score.")]
    public int targetScore = 150;

    [Tooltip("Nombre de flèches par manche.")]
    public int arrowsPerRound = 10;

    public string PlayerName => GameSettings.PlayerName;

    /// <summary>Résultat d'une flèche : ses points, centre ou non, ratée ou non.</summary>
    public struct Shot
    {
        public int points;
        public bool bullseye;
        public bool miss;
    }

    /// <summary>Les flèches tirées dans une manche (index 0 = manche 1), dans l'ordre.</summary>
    public IReadOnlyList<Shot> ShotsOfRound(int roundIndex) =>
        roundIndex >= 0 && roundIndex < roundShots.Count ? roundShots[roundIndex] : (IReadOnlyList<Shot>)Array.Empty<Shot>();

    /// <summary>Scores de toutes les manches (y compris celle en cours).</summary>
    public IReadOnlyList<int> RoundScores => roundScores;
    public int RoundNumber => roundScores.Count;                  // 1 = première manche
    public int RoundScore => roundScores.Count > 0 ? roundScores[^1] : 0;
    public int TotalScore { get; private set; }
    public int ArrowsShotThisRound { get; private set; }
    public int ArrowsLeft => Mathf.Max(0, arrowsPerRound - ArrowsShotThisRound);
    public bool IsRoundOver { get; private set; }
    public int RoundStartFrame { get; private set; }   // frame où la manche a commencé
    public bool TargetReached => RoundScore >= targetScore;

    // Statistiques globales (pour le classement plus tard)
    public int ArrowsShot { get; private set; }
    public int Hits { get; private set; }
    public int Bullseyes { get; private set; }
    public float Accuracy => ArrowsShot == 0 ? 0f : 100f * Hits / ArrowsShot;

    /// <summary>Appelé à chaque changement de score / flèches.</summary>
    public event Action OnScoreChanged;
    /// <summary>Appelé au début d'une manche (numéro de manche).</summary>
    public event Action<int> OnRoundStarted;
    /// <summary>Appelé à la fin d'une manche (numéro, score de la manche).</summary>
    public event Action<int, int> OnRoundEnded;

    readonly List<int> roundScores = new();
    readonly List<List<Shot>> roundShots = new();
    int arrowsInFlight;

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

    void Start() => StartNextRound();

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Manches ----------

    /// <summary>Démarre la manche suivante (bouton du tableau, ou script de niveau).</summary>
    public void StartNextRound()
    {
        roundScores.Add(0);
        roundShots.Add(new List<Shot>());
        ArrowsShotThisRound = 0;
        arrowsInFlight = 0;
        IsRoundOver = false;
        RoundStartFrame = Time.frameCount;

        Debug.Log($"[Score] Manche {RoundNumber} : {arrowsPerRound} flèches, objectif {targetScore} pts");
        OnRoundStarted?.Invoke(RoundNumber);
        OnScoreChanged?.Invoke();
    }

    void EndRound()
    {
        if (IsRoundOver) return;
        IsRoundOver = true;

        string reason = TargetReached ? "objectif atteint" : "plus de flèches";
        Debug.Log($"[Score] Fin de la manche {RoundNumber} ({reason}) : {RoundScore} pts");
        OnRoundEnded?.Invoke(RoundNumber, RoundScore);
        OnScoreChanged?.Invoke();
    }

    void CheckEndOfRound()
    {
        if (TargetReached) EndRound();
        else if (ArrowsLeft == 0 && arrowsInFlight == 0) EndRound();
    }

    // ---------- Appelés par les flèches / cibles ----------

    /// <summary>Une flèche part. Renvoie false si la manche est finie (le tir ne compte pas).</summary>
    public bool RegisterShot()
    {
        if (IsRoundOver || ArrowsLeft == 0) return false;

        ArrowsShotThisRound++;
        ArrowsShot++;
        arrowsInFlight++;
        OnScoreChanged?.Invoke();
        return true;
    }

    public void RegisterHit(int points, bool bullseye)
    {
        if (IsRoundOver) return;
        arrowsInFlight = Mathf.Max(0, arrowsInFlight - 1);

        Hits++;
        if (bullseye) Bullseyes++;
        roundScores[^1] += points;
        roundShots[^1].Add(new Shot { points = points, bullseye = bullseye });
        TotalScore += points;

        Debug.Log($"[Score] {PlayerName} : +{points} → {RoundScore}/{targetScore} pts (flèches restantes : {ArrowsLeft})");
        OnScoreChanged?.Invoke();
        CheckEndOfRound();
    }

    public void RegisterMiss()
    {
        if (IsRoundOver) return;
        arrowsInFlight = Mathf.Max(0, arrowsInFlight - 1);
        roundShots[^1].Add(new Shot { miss = true });

        Debug.Log($"[Score] Raté → {RoundScore}/{targetScore} pts (flèches restantes : {ArrowsLeft})");
        OnScoreChanged?.Invoke();
        CheckEndOfRound();
    }

    /// <summary>Remet tout à zéro (nouvelle partie).</summary>
    public void ResetGame()
    {
        roundScores.Clear();
        roundShots.Clear();
        TotalScore = ArrowsShot = Hits = Bullseyes = 0;
        StartNextRound();
    }
}
