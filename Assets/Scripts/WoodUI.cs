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
