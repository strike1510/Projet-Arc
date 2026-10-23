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

    [Header("Mode chronométré")]
    [Tooltip("Coché : flèches illimitées, la partie dure le temps du chrono (LevelTimer, 1 min 30) et s'arrête à zéro : " +
             "le score est figé et le niveau suivant est débloqué. Décoché : ancien mode (flèches comptées, objectif de points).")]
    public bool timedMode = true;

    [Header("Règles d'une manche (mode non chronométré)")]
    [Tooltip("La manche s'arrête dès qu'on atteint ce score.")]
    public int targetScore = 150;

    [Tooltip("Nombre de flèches par manche.")]
    public int arrowsPerRound = 10;

    [Header("Classement")]
    [Tooltip("Niveau utilisé pour le classement quand on lance la scène sans passer par le menu (0 = Niveau 1).")]
    public int levelIndexIfNoMenu = 0;

    [Header("Son de victoire")]
    [Tooltip("Musique jouée quand le score cible (targetScore) est atteint à la fin de la manche / du chrono.")]
    public AudioClip victoryClip;
    [Range(0f, 1f)] public float victoryVolume = 1f;
    [Tooltip("Objet avec un Audio Source (musique de fond) à mettre en pause pendant la victoire. Optionnel.")]
    public AudioSource backgroundMusic;

    public string PlayerName => GameSettings.PlayerName;

    /// <summary>Niveau en cours (0 = Niveau 1), pour le classement.</summary>
    public int LevelIndex => GameSettings.LevelIndex >= 0 ? GameSettings.LevelIndex : LevelFromSceneName();

    /// <summary>Lancé sans le menu : "Niveau3" → 2. Sinon levelIndexIfNoMenu.</summary>
    int LevelFromSceneName()
    {
        string scene = gameObject.scene.name;
        var digits = System.Text.RegularExpressions.Regex.Match(scene, @"\d+");
        return digits.Success && int.TryParse(digits.Value, out int n) && n > 0 ? n - 1 : levelIndexIfNoMenu;
    }

    string ArrowsInfo => timedMode ? $"{ArrowsShotThisRound} flèches tirées" : $"flèches restantes : {ArrowsLeft}";

    /// <summary>Vrai si la fin de cette partie vient de débloquer le niveau suivant.</summary>
    public bool NextLevelJustUnlocked { get; private set; }

    /// <summary>Vrai quand le total de cette partie a déjà été envoyé au classement.</summary>
    public bool GameSubmitted { get; private set; }

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
    public int ArrowsLeft => timedMode ? int.MaxValue : Mathf.Max(0, arrowsPerRound - ArrowsShotThisRound);
    public bool IsRoundOver { get; private set; }
    public int RoundStartFrame { get; private set; }   // frame où la manche a commencé
    public bool TargetReached => RoundScore >= targetScore;

    /// <summary>Vrai quand le temps du niveau (LevelTimer) est écoulé : plus de manche possible avant "Nouvelle partie".</summary>
    public bool IsTimeUp { get; private set; }

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
    /// <summary>Appelé quand on recommence une partie (bouton "Nouvelle partie").</summary>
    public event Action OnGameReset;
    /// <summary>Appelé quand le temps du niveau est écoulé.</summary>
    public event Action OnTimeUp;

    readonly List<int> roundScores = new();
    readonly List<List<Shot>> roundShots = new();
    int arrowsInFlight;
    AudioSource sfx;   // lecteur du son de victoire

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ScoreManager] Il y a deux ScoreManager dans la scène, le second est supprimé.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;   // 2D : s'entend partout
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
        if (IsTimeUp)
        {
            Debug.Log("[Score] Temps écoulé : pas de nouvelle manche (Nouvelle partie pour recommencer).");
            return;
        }

        // Arrête la musique de victoire et relance la musique de fond
        if (sfx != null) sfx.Stop();
        if (backgroundMusic != null) backgroundMusic.UnPause();

        roundScores.Add(0);
        roundShots.Add(new List<Shot>());
        ArrowsShotThisRound = 0;
        arrowsInFlight = 0;
        IsRoundOver = false;
        RoundStartFrame = Time.frameCount;

        Debug.Log(timedMode ? "[Score] Partie lancée : flèches illimitées, fais le meilleur score avant la fin du chrono." : $"[Score] Manche {RoundNumber} : {arrowsPerRound} flèches, objectif {targetScore} pts");
        OnRoundStarted?.Invoke(RoundNumber);
        OnScoreChanged?.Invoke();
    }

    void EndRound()
    {
        if (IsRoundOver) return;
        IsRoundOver = true;

        string reason = IsTimeUp ? "temps écoulé" : TargetReached ? "objectif atteint" : "plus de flèches";
        Debug.Log($"[Score] Fin de la manche {RoundNumber} ({reason}) : {RoundScore} pts");

        // Victoire : le score cible est atteint
        if (TargetReached) PlayVictory();

        OnRoundEnded?.Invoke(RoundNumber, RoundScore);
        OnScoreChanged?.Invoke();
    }

    void PlayVictory()
    {
        if (victoryClip == null) return;
        if (backgroundMusic != null) backgroundMusic.Pause();
        sfx.PlayOneShot(victoryClip, victoryVolume);
    }

    void CheckEndOfRound()
    {
        if (timedMode) return;   // mode chronométré : seule la fin du chrono arrête la partie
        if (TargetReached) EndRound();
        else if (ArrowsLeft == 0 && arrowsInFlight == 0) EndRound();
    }

    // ---------- Appelés par les flèches / cibles ----------

    /// <summary>Le chrono du niveau est à zéro : la manche en cours s'arrête et la partie est finie.</summary>
    public void TimeUp()
    {
        if (IsTimeUp) return;
        IsTimeUp = true;
        Debug.Log($"[Score] Temps écoulé ! Score final : {TotalScore} pts");

        // Score figé : enregistré au classement, et le niveau suivant s'ouvre
        SubmitGame();
        NextLevelJustUnlocked = LevelProgress.Unlock(LevelIndex + 1);
        OnTimeUp?.Invoke();
        if (!IsRoundOver) EndRound();
        else OnScoreChanged?.Invoke();
    }

    /// <summary>Flèche bonus (tir triple) : ne consomme pas de flèche, mais la manche attend qu'elle retombe.</summary>
    public bool RegisterBonusShot()
    {
        if (IsRoundOver) return false;
        arrowsInFlight++;
        return true;
    }

    /// <summary>Une flèche part. Renvoie false si la manche est finie (le tir ne compte pas).</summary>
    public bool RegisterShot()
    {
        if (IsRoundOver || (!timedMode && ArrowsLeft == 0)) return false;

        ArrowsShotThisRound++;
        ArrowsShot++;
        arrowsInFlight++;
        OnScoreChanged?.Invoke();
        return true;
    }

    public void RegisterHit(int points, bool bullseye) => RegisterHit(points, bullseye, false);

    /// <summary>Impact. 'bonus' = flèche du tir triple : ses points comptent, mais pas de rond en plus sur le panneau.</summary>
    public void RegisterHit(int points, bool bullseye, bool bonus)
    {
        if (IsRoundOver) return;
        arrowsInFlight = Mathf.Max(0, arrowsInFlight - 1);

        if (!bonus)
        {
            Hits++;
            if (bullseye) Bullseyes++;
            roundShots[^1].Add(new Shot { points = points, bullseye = bullseye });
        }
        roundScores[^1] += points;
        TotalScore += points;

        Debug.Log($"[Score] {PlayerName} : +{points} → {RoundScore} pts ({ArrowsInfo})");
        OnScoreChanged?.Invoke();
        CheckEndOfRound();
    }

    public void RegisterMiss() => RegisterMiss(false);

    public void RegisterMiss(bool bonus)
    {
        if (IsRoundOver) return;
        arrowsInFlight = Mathf.Max(0, arrowsInFlight - 1);
        if (bonus) { CheckEndOfRound(); return; }   // flèche bonus ratée : rien à afficher
        roundShots[^1].Add(new Shot { miss = true });

        Debug.Log($"[Score] Raté → {RoundScore} pts ({ArrowsInfo})");
        OnScoreChanged?.Invoke();
        CheckEndOfRound();
    }

    /// <summary>
    /// Enregistre le total de la partie dans le classement du niveau (une seule fois par partie).
    /// Renvoie la place obtenue (0 = premier) ou -1.
    /// </summary>
    public int SubmitGame()
    {
        if (GameSubmitted || TotalScore <= 0) return -1;
        GameSubmitted = true;
        return Leaderboard.Submit(LevelIndex, PlayerName, TotalScore);
    }

    /// <summary>Enregistre la partie puis revient au menu (scène de l'Open World).</summary>
    public void BackToMenu()
    {
        SubmitGame();
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameSettings.MenuScene);
    }

    // Casque retiré / application fermée en pleine partie : on garde quand même le score
    void OnApplicationQuit() => SubmitGame();

    /// <summary>Enregistre la partie en cours, puis remet tout à zéro (nouvelle partie).</summary>
    public void ResetGame()
    {
        SubmitGame();
        GameSubmitted = false;
        IsTimeUp = false;
        NextLevelJustUnlocked = false;
        IsRoundOver = false;
        OnGameReset?.Invoke();
        roundScores.Clear();
        roundShots.Clear();
        TotalScore = ArrowsShot = Hits = Bullseyes = 0;
        StartNextRound();
    }
}