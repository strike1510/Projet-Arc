[System.Serializable]
public class LevelInfo
{
    public string displayName = "Niveau 1";
    public string sceneName;      // scène à charger (vide = pas encore faite)
    public int requiredScore;     // score requis pour débloquer le niveau SUIVANT
}