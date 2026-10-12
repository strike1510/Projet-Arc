using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classement des meilleurs scores, un par niveau, sauvegardé sur l'appareil (PlayerPrefs).
/// Leaderboard.Get(0) = top du Niveau 1. Leaderboard.Submit(0, "GOOSE", 142) = enregistre une partie.
/// </summary>
public static class Leaderboard
{
    /// <summary>Nombre de scores gardés par niveau.</summary>
    public const int Size = 3;

    [Serializable]
    public class Entry
    {
        public string name;
        public int score;
        public string date;
    }

    [Serializable]
    class Table
    {
        public List<Entry> entries = new();
    }

    static string Key(int level) => $"Classement_Niveau{level + 1}";

    /// <summary>Les meilleurs scores d'un niveau, du meilleur au moins bon (au plus 'Size').</summary>
    public static List<Entry> Get(int level)
    {
        string json = PlayerPrefs.GetString(Key(level), "");
        if (string.IsNullOrEmpty(json)) return new List<Entry>();

        try
        {
            return JsonUtility.FromJson<Table>(json)?.entries ?? new List<Entry>();
        }
        catch (Exception)
        {
            Debug.LogWarning($"[Leaderboard] Classement du niveau {level + 1} illisible, remis à zéro.");
            return new List<Entry>();
        }
    }

    /// <summary>
    /// Enregistre le score d'une partie. Renvoie la place obtenue (0 = premier) ou -1 si hors du classement.
    /// </summary>
    public static int Submit(int level, string playerName, int score)
    {
        if (level < 0 || score <= 0) return -1;

        var entries = Get(level);
        var entry = new Entry { name = playerName, score = score, date = DateTime.Now.ToString("dd/MM/yyyy") };
        entries.Add(entry);

        // Tri du meilleur au moins bon ; à égalité, le plus ancien reste devant
        var sorted = new List<Entry>();
        foreach (var e in entries)
        {
            int i = 0;
            while (i < sorted.Count && sorted[i].score >= e.score) i++;
            sorted.Insert(i, e);
        }
        if (sorted.Count > Size) sorted.RemoveRange(Size, sorted.Count - Size);

        PlayerPrefs.SetString(Key(level), JsonUtility.ToJson(new Table { entries = sorted }));
        PlayerPrefs.Save();

        int rank = sorted.IndexOf(entry);
        Debug.Log(rank >= 0
            ? $"[Leaderboard] {playerName} : {score} pts → {rank + 1}e du niveau {level + 1}"
            : $"[Leaderboard] {playerName} : {score} pts, hors du top {Size} du niveau {level + 1}");
        return rank;
    }

    /// <summary>Efface le classement d'un niveau.</summary>
    public static void Clear(int level)
    {
        PlayerPrefs.DeleteKey(Key(level));
        PlayerPrefs.Save();
    }
}
