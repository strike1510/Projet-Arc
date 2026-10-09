using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Outils partagés pour les panneaux en bois (tableau des scores, etc.) :
/// texture bois procédurale, création de textes / boutons, EventSystem compatible VR.
/// </summary>
public static class WoodUI
{
    public static readonly Color WoodLight = new Color(0.62f, 0.42f, 0.24f);
    public static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.11f);
    public static readonly Color Ink = new Color(0.20f, 0.11f, 0.05f);
    public static readonly Color Cream = new Color(1f, 0.94f, 0.80f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    public static readonly Color Green = new Color(0.55f, 0.95f, 0.45f);

    /// <summary>Couleur d'une flèche selon son résultat (même code couleur que le popup de la cible).</summary>
    public static Color ShotColor(int points, bool bullseye, bool miss = false)
    {
        if (miss) return new Color(0.45f, 0.42f, 0.40f);              // gris : ratée
        if (bullseye) return new Color(1f, 0.84f, 0f);                // or : centre
        if (points >= 8) return new Color(1f, 0.35f, 0.25f);          // rouge vif
        if (points >= 4) return new Color(0.45f, 0.85f, 1f);          // bleu clair
        if (points > 0) return Color.white;
        return new Color(0.6f, 0.6f, 0.6f);
    }

    static Sprite circle, ring;

    /// <summary>Disque plein (pastille).</summary>
    public static Sprite CircleSprite => circle != null ? circle : circle = MakeSprite(MakeCircleTexture(64, 1f));

    /// <summary>Cercle vide (flèche pas encore tirée).</summary>
    public static Sprite RingSprite => ring != null ? ring : ring = MakeSprite(MakeCircleTexture(64, 0.22f));

    static Texture2D MakeCircleTexture(int size, float thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        float r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
            float outer = Mathf.Clamp01(r - d + 0.5f);                          // bord extérieur lissé
            float inner = Mathf.Clamp01(d - r * (1f - thickness) + 0.5f);       // trou au centre (anneau)
            px[y * size + x] = new Color(1f, 1f, 1f, outer * (thickness >= 1f ? 1f : inner));
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Une rangée de pastilles, une par flèche : couleur = résultat, cercle vide = pas encore tirée.
    /// </summary>
    public static void ShotDots(Transform parent, IReadOnlyList<ScoreManager.Shot> shots, int total,
        float centerY, float dotSize, float gap)
    {
        float width = total * dotSize + (total - 1) * gap;
        float x = -width / 2f + dotSize / 2f;
        for (int i = 0; i < total; i++, x += dotSize + gap)
        {
            if (i < shots.Count)
            {
                var s = shots[i];
                NewImage(parent, "Flèche " + (i + 1), new Vector2(x, centerY), Vector2.one * dotSize, CircleSprite,
                    ShotColor(s.points, s.bullseye, s.miss));
                if (s.miss)   // une petite croix sombre sur les ratées
                    NewText(parent, "x", new Vector2(x, centerY), Vector2.one * dotSize, dotSize * 0.8f,
                        new Color(0.15f, 0.1f, 0.08f), TextAlignmentOptions.Center, FontStyles.Bold);
            }
            else
            {
                NewImage(parent, "Flèche " + (i + 1), new Vector2(x, centerY), Vector2.one * dotSize, RingSprite,
                    new Color(1f, 0.94f, 0.8f, 0.55f));
            }
        }
    }

    /// <summary>
    /// Légende des couleurs des pastilles (points de chaque couleur) : 10 / 8 / 4-6 / 2 / Raté, sur une ligne.
    /// </summary>
    public static void ShotLegend(Transform parent, float centerY, float width, float dotSize, float fontSize)
    {
        string[] labels = { "10", "8", "4-6", "2", "Raté" };
        Color[] colors =
        {
            ShotColor(10, true), ShotColor(8, false), ShotColor(6, false), ShotColor(2, false), ShotColor(0, false, true)
        };

        float slot = width / labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            float left = -width / 2f + i * slot;
            float dotX = left + dotSize / 2f + 6f;
            NewImage(parent, "Légende " + labels[i], new Vector2(dotX, centerY), Vector2.one * dotSize, CircleSprite, colors[i]);
            if (i == labels.Length - 1)
                NewText(parent, "x", new Vector2(dotX, centerY), Vector2.one * dotSize, dotSize * 0.8f,
                    new Color(0.15f, 0.1f, 0.08f), TextAlignmentOptions.Center, FontStyles.Bold);

            float textW = slot - dotSize - 14f;
            NewText(parent, labels[i], new Vector2(left + dotSize + 12f + textW / 2f, centerY), new Vector2(textW, dotSize * 1.6f),
                fontSize, Cream, TextAlignmentOptions.Left);
        }
    }

    /// <summary>S'assure qu'il y a un EventSystem utilisable avec les manettes VR (et la souris).</summary>
    public static void EnsureXREventSystem()
    {
        var es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        if (es.GetComponent<XRUIInputModule>() != null) return;

        foreach (var m in es.GetComponents<BaseInputModule>()) Object.DestroyImmediate(m);
        es.gameObject.AddComponent<XRUIInputModule>();
    }

    public static Sprite MakeSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

    public static Material MakeWoodMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { mainTexture = MakeWoodTexture(256, 256, 4, 7f, false) };
        mat.color = new Color(0.85f, 0.85f, 0.85f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        return mat;
    }

    /// <summary>Texture de planches horizontales, avec joints et cadre sombre optionnel.</summary>
    public static Texture2D MakeWoodTexture(int w, int h, int planks, float seed, bool frame)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[w * h];
        float plankH = (float)h / planks;
        int border = Mathf.Max(3, w / 64);

        for (int y = 0; y < h; y++)
        {
            int p = Mathf.Min(planks - 1, (int)(y / plankH));
            float py = (y - p * plankH) / plankH;

            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.012f + seed + p * 13.7f, y * 0.05f + p * 3.1f);
                float grain = Mathf.Pow(Mathf.Sin(y * 0.45f + n * 20f + p * 5f) * 0.5f + 0.5f, 3f);
                float fine = Mathf.PerlinNoise(x * 0.25f + seed, y * 1.7f);
                Color c = Color.Lerp(WoodLight, WoodDark, 0.55f * grain + 0.25f * fine + 0.2f * n);
                c *= 0.88f + 0.24f * Mathf.PerlinNoise(p * 7.3f + seed, 0.5f);

                if (planks > 1 && (py < 0.02f || py > 0.98f)) c *= 0.45f;
                if (frame && (x < border || y < border || x >= w - border || y >= h - border)) c *= 0.55f;

                c.a = 1f;
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------- Création d'éléments UI ----------

    public static RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);   // ancré en haut au centre
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    public static Image NewImage(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        var img = NewRect(parent, name, pos, size).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI NewText(Transform parent, string text, Vector2 pos, Vector2 size, float fontSize,
        Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
    {
        var t = NewRect(parent, "Texte", pos, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    public static Button NewButton(Transform parent, string label, Vector2 pos, Vector2 size, float fontSize,
        Sprite sprite, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage(parent, "Bouton " + label, pos, size, sprite, Color.white);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f);
        colors.pressedColor = new Color(0.65f, 0.55f, 0.45f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1.3f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        var text = NewText(img.transform, label, Vector2.zero, size, fontSize, Cream, TextAlignmentOptions.Center, FontStyles.Bold);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        return btn;
    }
}
