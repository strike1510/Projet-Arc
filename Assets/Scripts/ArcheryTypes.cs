/// <summary>
/// Les 4 familles d'arcs / flèches du projet, reconnues à leur nom (Bow_Monkey, Arrow_Bio (1), ...).
/// </summary>
public static class ArcheryTypes
{
    public const string Base = "Base";
    public const string Bio = "Bio";
    public const string Fulminaleuse = "Fulminaleuse";
    public const string Monkey = "Monkey";

    public static readonly string[] All = { Base, Bio, Fulminaleuse, Monkey };

    /// <summary>Famille d'après le nom de l'objet ("Base" si rien ne correspond).</summary>
    public static string Of(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return Base;
        if (objectName.Contains(Monkey)) return Monkey;
        if (objectName.Contains(Fulminaleuse)) return Fulminaleuse;
        if (objectName.Contains(Bio)) return Bio;
        return Base;
    }
}
