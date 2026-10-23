using UnityEngine;

/// <summary>
/// Progression du joueur : niveaux débloqués, sauvegardés sur l'appareil (PlayerPrefs).
/// Le Niveau 1 est toujours ouvert ; finir la partie chronométrée d'un niveau débloque le suivant.
/// </summary>
public static class LevelProgress
{
    const string Key = "Progression_NiveauxDebloques";

    /// <summary>Nombre de niveaux ouverts (1 = seulement le Niveau 1).</summary>
    public static int Unlocked => Mathf.Max(1, PlayerPrefs.GetInt(Key, 1));

    /// <summary>Vrai si le niveau 'index' (0 = Niveau 1) est jouable.</summary>
    public static bool IsUnlocked(int index) => index >= 0 && index < Unlocked;

    /// <summary>Ouvre le niveau 'index'. Renvoie true s'il vient d'être débloqué.</summary>
    public static bool Unlock(int index)
    {
        if (index < 0 || index < Unlocked) return false;
        PlayerPrefs.SetInt(Key, index + 1);
        PlayerPrefs.Save();
        Debug.Log($"[Progression] Niveau {index + 1} débloqué.");
        return true;
    }

    /// <summary>Remet la progression à zéro (seul le Niveau 1 est ouvert).</summary>
    public static void Reset()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
