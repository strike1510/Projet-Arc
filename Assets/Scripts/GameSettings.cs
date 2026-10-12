/// <summary>
/// Choix faits dans le menu, conservés quand on change de scène.
/// Lecture depuis n'importe quel script : GameSettings.PlayerName, GameSettings.LevelIndex, etc.
/// </summary>
public static class GameSettings
{
    /// <summary>Prénom saisi dans le menu.</summary>
    public static string PlayerName = "Joueur";

    /// <summary>Niveau choisi : 0 = Niveau 1, 1 = Niveau 2, 2 = Niveau 3. -1 = aucun (lancé sans passer par le menu).</summary>
    public static int LevelIndex = -1;

    /// <summary>Nom affiché du niveau choisi (ex. "Niveau 1").</summary>
    public static string LevelName = "";

    /// <summary>Scène du menu, pour y revenir depuis un niveau (bouton "Menu").</summary>
    public static string MenuScene = "Open_World";
}
