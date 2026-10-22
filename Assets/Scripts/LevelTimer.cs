using TMPro;
using UnityEngine;

/// <summary>
/// Chrono du niveau (5 min par défaut). À zéro, la manche en cours s'arrête et la partie est finie
/// (ScoreManager.TimeUp) : il reste "Nouvelle partie" ou "Menu" sur le panneau des résultats.
/// "Nouvelle partie" remet le chrono à 5 min.
/// Les textes qui affichent le temps sont de vrais objets de la scène (montre au poignet gauche, panneau).
/// </summary>
public class LevelTimer : MonoBehaviour
{
    [Tooltip("Temps du niveau en secondes (300 = 5 min).")]
    public float duration = 300f;

    [Tooltip("Le chrono s'arrête entre deux manches (pendant qu'on lit le panneau).")]
    public bool pauseBetweenRounds = true;

    [Tooltip("Textes qui affichent le temps restant.")]
    public TMP_Text[] displays;

    [Tooltip("{0} = minutes, {1} = secondes")]
    public string format = "{0}:{1:00}";
    public string pausedSuffix = "  (pause)";
    public string timeUpText = "Temps écoulé !";

    [Header("Couleurs")]
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.3f, 0.25f);
    [Tooltip("En dessous de ce temps (s), le texte passe en rouge et clignote.")]
    public float warningTime = 30f;

    /// <summary>Temps restant (s).</summary>
    public float Remaining { get; private set; }

    ScoreManager subscribedTo;

    void Awake() => Remaining = duration;

    void OnDestroy()
    {
        if (subscribedTo != null) subscribedTo.OnGameReset -= ResetTimer;
    }

    void ResetTimer() => Remaining = duration;

    void Update()
    {
        var sm = ScoreManager.Instance;
        if (sm != null && subscribedTo != sm)
        {
            if (subscribedTo != null) subscribedTo.OnGameReset -= ResetTimer;
            sm.OnGameReset += ResetTimer;
            subscribedTo = sm;
        }

        bool paused = sm != null && pauseBetweenRounds && sm.IsRoundOver;
        bool running = Remaining > 0f && (sm == null || !sm.IsTimeUp) && !paused;

        if (running)
        {
            Remaining -= Time.deltaTime;
            if (Remaining <= 0f)
            {
                Remaining = 0f;
                if (sm != null) sm.TimeUp();
            }
        }

        Refresh(paused && Remaining > 0f);
    }

    void Refresh(bool paused)
    {
        if (displays == null) return;

        int total = Mathf.CeilToInt(Remaining);
        bool timeUp = Remaining <= 0f;
        string text = timeUp ? timeUpText : string.Format(format, total / 60, total % 60) + (paused ? pausedSuffix : "");

        bool warning = timeUp || Remaining <= warningTime;
        Color color = warning ? warningColor : normalColor;
        if (warning && !timeUp && !paused) color.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f));   // clignote

        foreach (var d in displays)
        {
            if (d == null) continue;
            if (d.text != text) d.text = text;
            d.color = color;
        }
    }
}
