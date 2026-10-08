using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Menu principal VR : panneau en bois flottant avec saisie du nom (clavier virtuel),
/// choix de la difficulté et bouton Jouer.
/// Usage : un GameObject vide dans la scène Menu + ce script. Tout le reste est généré au lancement.
/// Le joueur regarde vers +Z : place l'objet ~1.5 m devant lui, à hauteur des yeux.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scènes (toutes sur la même tant qu'il n'y a qu'un niveau)")]
    public string sceneFacile = "SampleScene";
    public string sceneNormal = "SampleScene";
    public string sceneDifficile = "SampleScene";

    [Header("Nom")]
    public int maxNameLength = 12;

    [Header("Flottement")]
    public float bobAmplitude = 0.03f;
    public float bobSpeed = 1.2f;
    public float swayDegrees = 1.5f;

    // Taille du panneau (unités du canvas ; 1 unité = 1.2 mm)
    const float W = 1000f, H = 860f, PixelToMeter = 0.0012f;

    static readonly Color WoodLight = new Color(0.62f, 0.42f, 0.24f);
    static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.11f);
    static readonly Color Ink = new Color(0.20f, 0.11f, 0.05f);
    static readonly Color Cream = new Color(1f, 0.94f, 0.80f);
    static readonly Color Selected = new Color(0.70f, 1f, 0.62f);

    Transform board;
    Vector3 boardBasePos;
    TextMeshProUGUI nameText, messageText;
    readonly Dictionary<Difficulty, Image> difficultyButtons = new();
    Sprite plankSprite, keySprite;
    string playerName = "";

    void Start()
    {
        EnsureXREventSystem();

        plankSprite = MakeSprite(MakeWoodTexture(512, 440, 5, 0f, true));
        keySprite = MakeSprite(MakeWoodTexture(128, 64, 1, 42f, false));

        BuildBoard();
        BuildUI();
        SelectDifficulty(GameSettings.Difficulty);

        if (GameSettings.PlayerName != "Joueur") playerName = GameSettings.PlayerName;
    }

    void OnEnable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnPhysicalKey;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnPhysicalKey;
    }

    void Update()
    {
        if (board != null)
        {
            float t = Time.time * bobSpeed;
            board.localPosition = boardBasePos + Vector3.up * Mathf.Sin(t) * bobAmplitude;
            board.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * swayDegrees);
        }

        if (nameText != null)
        {
            bool cursor = playerName.Length < maxNameLength && (Time.time % 1f) < 0.5f;
            nameText.text = playerName + (cursor ? "_" : " ");
        }
    }

    // ---------- Actions ----------

    void TypeChar(char c)
    {
        if (playerName.Length >= maxNameLength) return;
        playerName += c;
        messageText.text = "";
    }

    void Backspace()
    {
        if (playerName.Length > 0) playerName = playerName.Substring(0, playerName.Length - 1);
    }

    void OnPhysicalKey(char c)
    {
        if (c == '\b') Backspace();
        else if (char.IsLetterOrDigit(c) || c == ' ' || c == '-') TypeChar(char.ToUpper(c));
    }

    void SelectDifficulty(Difficulty d)
    {
        GameSettings.Difficulty = d;
        foreach (var kv in difficultyButtons)
            kv.Value.color = kv.Key == d ? Selected : Color.white;
    }

    void Play()
    {
        string name = playerName.Trim();
        if (name.Length == 0)
        {
            messageText.text = "Entre ton nom d'abord !";
            return;
        }

        GameSettings.PlayerName = name;

        string scene = GameSettings.Difficulty switch
        {
            Difficulty.Facile => sceneFacile,
            Difficulty.Difficile => sceneDifficile,
            _ => sceneNormal
        };

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            messageText.text = "Scène introuvable : " + scene;
            Debug.LogError($"[MainMenu] La scène '{scene}' n'est pas dans File > Build Profiles > Scene List.");
            return;
        }

        SceneManager.LoadScene(scene);
    }

    // ---------- Construction ----------

    void BuildBoard()
    {
        board = new GameObject("Panneau").transform;
        board.SetParent(transform, false);
        boardBasePos = Vector3.zero;

        // Épaisseur du panneau (cube en bois derrière le canvas)
        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slab.name = "Planche";
        Destroy(slab.GetComponent<Collider>());
        slab.transform.SetParent(board, false);
        slab.transform.localScale = new Vector3(W * PixelToMeter + 0.04f, H * PixelToMeter + 0.04f, 0.05f);
        slab.transform.localPosition = new Vector3(0f, 0f, 0.03f);

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader) { mainTexture = MakeWoodTexture(256, 256, 4, 7f, false) };
        mat.color = new Color(0.85f, 0.85f, 0.85f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        slab.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("MenuCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(board, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = (RectTransform)canvasGO.transform;
        rt.sizeDelta = new Vector2(W, H);
        rt.localScale = Vector3.one * PixelToMeter;

        // Fond en planches
        var bg = NewImage(rt, "Fond", Vector2.zero, new Vector2(W, H), plankSprite, Color.white);
        bg.raycastTarget = false;

        NewText(rt, "VR ARCHERY CHALLENGE", new Vector2(0, 375), new Vector2(W, 80), 58, Cream, FontStyles.Bold);

        // Nom
        NewText(rt, "Ton nom :", new Vector2(0, 300), new Vector2(W, 50), 36, Cream);
        var field = NewImage(rt, "ChampNom", new Vector2(0, 240), new Vector2(560, 70), null, Cream);
        field.raycastTarget = false;
        nameText = NewText((RectTransform)field.transform, "", Vector2.zero, new Vector2(540, 70), 42, Ink, FontStyles.Bold);

        // Clavier virtuel AZERTY
        string[] rows = { "1234567890", "AZERTYUIOP", "QSDFGHJKLM", "WXCVBN" };
        const float key = 74f, gap = 8f;
        float y = 150f;
        for (int r = 0; r < rows.Length; r++, y -= key - 4f)
        {
            string row = rows[r];
            int extra = r == rows.Length - 1 ? 2 : 0;       // dernière ligne : + Espace + Effacer (2 cases chacune)
            float rowWidth = (row.Length + extra * 2) * (key + gap) - gap;
            float x = -rowWidth / 2f + key / 2f;

            foreach (char c in row)
            {
                char ch = c;
                NewButton(rt, c.ToString(), new Vector2(x, y), new Vector2(key, key - 10f), 34, () => TypeChar(ch));
                x += key + gap;
            }

            if (extra > 0)
            {
                float wide = key * 2 + gap;
                x += wide / 2f - key / 2f;
                NewButton(rt, "Espace", new Vector2(x, y), new Vector2(wide, key - 10f), 28, () => TypeChar(' '));
                x += wide + gap;
                NewButton(rt, "Effacer", new Vector2(x, y), new Vector2(wide, key - 10f), 28, Backspace);
            }
        }

        // Difficulté
        NewText(rt, "Difficulté :", new Vector2(0, -150), new Vector2(W, 50), 36, Cream);
        float dx = -230f;
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        {
            Difficulty diff = d;
            var b = NewButton(rt, d.ToString(), new Vector2(dx, -215), new Vector2(210, 70), 34, () => SelectDifficulty(diff));
            difficultyButtons[d] = b.GetComponent<Image>();
            dx += 230f;
        }

        // Jouer
        NewButton(rt, "JOUER", new Vector2(0, -320), new Vector2(320, 90), 50, Play, FontStyles.Bold);
        messageText = NewText(rt, "", new Vector2(0, -395), new Vector2(W, 40), 30, new Color(1f, 0.55f, 0.45f));
    }

    static void EnsureXREventSystem()
    {
        var es = FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
        if (es.GetComponent<XRUIInputModule>() != null) return;

        foreach (var m in es.GetComponents<BaseInputModule>()) DestroyImmediate(m);
        es.gameObject.AddComponent<XRUIInputModule>();
    }

    // ---------- Helpers UI ----------

    Image NewImage(RectTransform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    TextMeshProUGUI NewText(RectTransform parent, string text, Vector2 pos, Vector2 size, float fontSize, Color color,
        FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Texte", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    Button NewButton(RectTransform parent, string label, Vector2 pos, Vector2 size, float fontSize,
        UnityEngine.Events.UnityAction onClick, FontStyles style = FontStyles.Bold)
    {
        var img = NewImage(parent, "Bouton " + label, pos, size, keySprite, Color.white);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.15f, 0.9f);
        colors.pressedColor = new Color(0.65f, 0.55f, 0.45f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 1.3f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        NewText((RectTransform)img.transform, label, Vector2.zero, size, fontSize, Cream, style);
        return btn;
    }

    // ---------- Texture bois procédurale ----------

    static Sprite MakeSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

    static Texture2D MakeWoodTexture(int w, int h, int planks, float seed, bool frame)
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
                c *= 0.88f + 0.24f * Mathf.PerlinNoise(p * 7.3f + seed, 0.5f);   // teinte par planche

                if (planks > 1 && (py < 0.02f || py > 0.98f)) c *= 0.45f;        // joints entre planches
                if (frame && (x < border || y < border || x >= w - border || y >= h - border)) c *= 0.55f;

                c.a = 1f;
                px[y * w + x] = c;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
