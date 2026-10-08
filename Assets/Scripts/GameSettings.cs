using UnityEngine;

public enum Difficulty { Facile, Normal, Difficile }

/// <summary>
/// Choix faits dans le menu, conservés quand on change de scène.
/// Lecture depuis n'importe quel script : GameSettings.PlayerName, GameSettings.Difficulty
/// </summary>
public static class GameSettings
{
    public static string PlayerName = "Joueur";
    public static Difficulty Difficulty = Difficulty.Normal;

    // Paramètres prêts à l'emploi pour quand les niveaux existeront
    public static float TargetScale => Difficulty switch
    {
        Difficulty.Facile => 1.5f,
        Difficulty.Difficile => 0.6f,
        _ => 1f
    };

    public static float TargetSpeed => Difficulty switch
    {
        Difficulty.Facile => 0.5f,
        Difficulty.Difficile => 2.5f,
        _ => 1.2f
    };

    public static float TimeLimit => Difficulty switch
    {
        Difficulty.Facile => 120f,
        Difficulty.Difficile => 60f,
        _ => 90f
    };
}
