using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Tableau des scores en bois qui flotte.
/// Affiche le prénom, puis une ligne par manche ("Manche 1 ... 46 pts"), qui s'ajoute en dessous à chaque nouvelle manche.
/// Par défaut il est caché pendant le jeu et apparaît devant le joueur à la fin de chaque manche.
/// Usage : un GameObject vide dans la scène + ce script (sa position n'est utilisée que si
/// 'appearInFrontOfPlayer' est décoché).
/// </summary>
public class ScoreBoard : MonoBehaviour
{
    [Header("Apparition")]
    [Tooltip("Caché pendant la manche, affiché seulement à la fin.")]
    public bool showOnlyAtRoundEnd = true;

    [Tooltip("Apparaît devant le joueur, là où il regarde. Sinon, reste à la position de l'objet.")]
    public bool appearInFrontOfPlayer = true;

    [Tooltip("Distance devant le joueur (m).")]
    public float distanceFromPlayer = 2f;

    [Tooltip("Hauteur du haut du tableau par rapport aux yeux (m).")]
    public float heightAboveEyes = 0.4f;

    public float appearDuration = 0.45f;

    [Header("Orientation")]
    [Tooltip("Le tableau tourne pour toujours faire face au joueur.")]
    public bool facePlayer = true;

    [Header("Flottement")]
    public float bobAmplitude = 0.03f;
    public float bobSpeed = 1.1f;
    public float swayDegrees = 1.2f;

    // Taille du tableau en unités du canvas (1 unité = 1.5 mm)
    const float W = 760f, PixelToMeter = 0.0015f, Pad = 40f;

    Transform board;
    RectTransform canvasRect, content;
    Image background;
    Transform slab;
    Sprite buttonSprite;
    float currentHeight = -1f;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        WoodUI.EnsureXREventSystem();
        buttonSprite = WoodUI.MakeSprite(WoodUI.MakeWoodTexture(128, 64, 1, 42f, false));
        Build();

        var sm = ScoreManager.Instance;
        if (sm == null)
        {
            Debug.LogError("[ScoreBoard] Pas de ScoreManager dans la scène.");
            return;
        }
        sm.OnScoreChanged += Refresh;
        sm.OnRoundEnded += OnRoundEnded;
        sm.OnRoundStarted += OnRoundStarted;
        Refresh();

        if (showOnlyAtRoundEnd && !sm.IsRoundOver) board.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        var sm = ScoreManager.Instance;
        if (sm == null) return;
        sm.OnScoreChanged -= Refresh;
        sm.OnRoundEnded -= OnRoundEnded;
        sm.OnRoundStarted -= OnRoundStarted;
    }

    void OnRoundEnded(int round, int score) => Show();

    void OnRoundStarted(int round)
    {
        if (showOnlyAtRoundEnd && board != null) board.gameObject.SetActive(false);
    }

    /// <summary>Fait apparaître le tableau (devant le joueur si demandé), avec une petite animation.</summary>
    public void Show()
    {
        if (board == null) return;

        if (appearInFrontOfPlayer && cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            transform.position = cam.transform.position + forward * distanceFromPlayer + Vector3.up * heightAboveEyes;
            transform.rotation = Quaternion.LookRotation(forward);
        }

        board.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(PopIn());
    }

    System.Collections.IEnumerator PopIn()
    {
        for (float t = 0f; t < appearDuration; t += Time.deltaTime)
        {
            float k = t / appearDuration;
            // "ease out back" : grossit, dépasse un peu, puis se pose
            float s = 1f + 2.7f * Mathf.Pow(k - 1f, 3f) + 1.7f * Mathf.Pow(k - 1f, 2f);
            board.localScale = Vector3.one * Mathf.Max(0.01f, s);
            yield return null;
        }
        board.localScale = Vector3.one;
    }

    void Update()
    {
        if (board == null) return;

        float t = Time.time * bobSpeed;
        board.localPosition = Vector3.up * (Mathf.Sin(t) * bobAmplitude);
        board.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * swayDegrees);

        if (facePlayer && cam != null)
        {
            Vector3 dir = transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 3f * Time.deltaTime);
        }
    }

    // ---------- Construction du cadre ----------

    void Build()
    {
        board = new GameObject("Panneau").transform;
        board.SetParent(transform, false);

        slab = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        slab.name = "Planche";
        Destroy(slab.GetComponent<Collider>());
        slab.SetParent(board, false);
        slab.GetComponent<Renderer>().sharedMaterial = WoodUI.MakeWoodMaterial();

        var canvasGO = new GameObject("ScoreCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(board, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();   // clics avec les manettes VR
        canvasGO.AddComponent<GraphicRaycaster>();                // clics à la souris (tests sans casque)

        canvasRect = (RectTransform)canvasGO.transform;
        canvasRect.pivot = new Vector2(0.5f, 1f);          // le tableau pousse vers le bas
        canvasRect.localScale = Vector3.one * PixelToMeter;

        background = WoodUI.NewImage(canvasRect, "Fond", Vector2.zero, Vector2.zero, null, Color.white);
        var br = background.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = Vector2.one;
        br.sizeDelta = Vector2.zero;

        content = WoodUI.NewRect(canvasRect, "Contenu", Vector2.zero, Vector2.zero);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.sizeDelta = Vector2.zero;
    }

    // ---------- Contenu ----------

    void Refresh()
    {
        var sm = ScoreManager.Instance;
        if (sm == null || content == null) return;

        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        float y = -Pad;
        const float inner = W - 2 * Pad;

        // Prénom
        WoodUI.NewText(content, sm.PlayerName, new Vector2(0, y - 45), new Vector2(inner, 90), 72,
            WoodUI.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
        y -= 100;
        Separator(ref y);

        // Une ligne par manche
        for (int i = 0; i < sm.RoundScores.Count; i++)
        {
            int round = i + 1;
            int score = sm.RoundScores[i];
            bool last = round == sm.RoundNumber;
            Color color = score >= sm.targetScore ? WoodUI.Green : WoodUI.Cream;

            WoodUI.NewText(content, $"Manche {round}", new Vector2(0, y - 35), new Vector2(inner, 70), 50,
                color, TextAlignmentOptions.Left);
            WoodUI.NewText(content, $"{score} pts", new Vector2(0, y - 35), new Vector2(inner, 70), 50,
                color, TextAlignmentOptions.Right, FontStyles.Bold);
            y -= 70;

            if (last) Details(sm, ref y);
        }

        // Total si plusieurs manches
        if (sm.RoundScores.Count > 1)
        {
            Separator(ref y);
            WoodUI.NewText(content, "Total", new Vector2(0, y - 35), new Vector2(inner, 70), 54,
                WoodUI.Gold, TextAlignmentOptions.Left, FontStyles.Bold);
            WoodUI.NewText(content, $"{sm.TotalScore} pts", new Vector2(0, y - 35), new Vector2(inner, 70), 54,
                WoodUI.Gold, TextAlignmentOptions.Right, FontStyles.Bold);
            y -= 80;
        }

        // Fin de manche
        if (sm.IsRoundOver)
        {
            y -= 10;
            string msg = sm.TargetReached ? "Objectif atteint !" : "Plus de flèches !";
            WoodUI.NewText(content, $"Manche {sm.RoundNumber} terminée\n<size=80%>{msg}</size>", new Vector2(0, y - 45),
                new Vector2(inner, 90), 40, WoodUI.Gold);
            y -= 105;
            WoodUI.NewButton(content, "Manche suivante", new Vector2(-170, y - 40), new Vector2(320, 80), 36,
                buttonSprite, () => ScoreManager.Instance.StartNextRound());
            WoodUI.NewButton(content, "Nouvelle partie", new Vector2(170, y - 40), new Vector2(320, 80), 36,
                buttonSprite, () => ScoreManager.Instance.ResetGame());
            y -= 100;
        }

        Resize(-y + Pad);
    }

    void Details(ScoreManager sm, ref float y)
    {
        const float inner = W - 2 * Pad;

        WoodUI.NewText(content, $"Flèches : {sm.ArrowsLeft} / {sm.arrowsPerRound}", new Vector2(0, y - 22),
            new Vector2(inner, 44), 32, WoodUI.Cream * 0.9f, TextAlignmentOptions.Left);
        WoodUI.NewText(content, $"Objectif : {Mathf.Min(sm.RoundScore, sm.targetScore)} / {sm.targetScore}",
            new Vector2(0, y - 22), new Vector2(inner, 44), 32, WoodUI.Cream * 0.9f, TextAlignmentOptions.Right);
        y -= 52;

        // Barre de progression vers l'objectif
        float progress = Mathf.Clamp01((float)sm.RoundScore / sm.targetScore);
        WoodUI.NewImage(content, "Barre fond", new Vector2(0, y - 11), new Vector2(inner, 22), null, new Color(0.15f, 0.08f, 0.03f, 0.85f));
        if (progress > 0f)
        {
            float w = inner * progress;
            WoodUI.NewImage(content, "Barre", new Vector2(-inner / 2f + w / 2f, y - 11), new Vector2(w, 22), null,
                progress >= 1f ? WoodUI.Green : WoodUI.Gold);
        }
        y -= 44;
    }

    void Separator(ref float y)
    {
        WoodUI.NewImage(content, "Séparateur", new Vector2(0, y - 3), new Vector2(W - 2 * Pad, 6), null,
            new Color(0.15f, 0.08f, 0.03f, 0.8f));
        y -= 24;
    }

    void Resize(float height)
    {
        canvasRect.sizeDelta = new Vector2(W, height);

        float wm = W * PixelToMeter, hm = height * PixelToMeter;
        slab.localScale = new Vector3(wm + 0.04f, hm + 0.04f, 0.05f);
        slab.localPosition = new Vector3(0f, -hm / 2f, 0.03f);

        // Regénère la texture des planches seulement si la hauteur a changé (évite les planches étirées)
        if (!Mathf.Approximately(height, currentHeight))
        {
            currentHeight = height;
            int texH = Mathf.Clamp(Mathf.RoundToInt(512 * height / W), 64, 1024);
            int planks = Mathf.Max(2, Mathf.RoundToInt(height / 110f));
            if (background.sprite != null) Destroy(background.sprite.texture);
            background.sprite = WoodUI.MakeSprite(WoodUI.MakeWoodTexture(512, texH, planks, 0f, true));
        }
    }
}
