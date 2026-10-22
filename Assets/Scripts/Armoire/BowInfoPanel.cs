using TMPro;
using UnityEngine;

/// <summary>
/// Panneau en bois qui flotte devant un arc du sélecteur : nom, description, stats, effet spécial.
/// Créé automatiquement par BowSummoner (même style que les autres panneaux, via WoodUI).
/// L'arc lui donne sa position à chaque image ; il se tourne tout seul vers le joueur.
/// </summary>
public class BowInfoPanel : MonoBehaviour
{
    const float W = 560f, H = 420f;

    public bool faceCamera = true;

    CanvasGroup content;
    Vector3 target, fullScale;
    bool visible, placed;
    float showAt, phase;

    public static BowInfoPanel Create(BowData data, float scale, float speedRef, float drawRef)
    {
        var go = new GameObject("Panneau " + data.displayName, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(W, H);

        var panel = go.AddComponent<BowInfoPanel>();
        panel.fullScale = Vector3.one * scale;
        panel.phase = Random.value * 10f;
        panel.Build(data, speedRef, drawRef);
        rt.localScale = Vector3.zero;
        go.SetActive(false);
        return panel;
    }

    void Build(BowData d, float speedRef, float drawRef)
    {
        var root = WoodUI.NewRect(transform, "Contenu", new Vector2(0, -H / 2), new Vector2(W, H));
        content = root.gameObject.AddComponent<CanvasGroup>();
        content.interactable = false;
        content.blocksRaycasts = false;

        var wood = WoodUI.MakeSprite(WoodUI.MakeWoodTexture(256, 192, 3, Random.Range(0f, 100f), true));
        WoodUI.NewImage(root, "Fond", new Vector2(0, -H / 2), new Vector2(W, H), wood, Color.white);

        WoodUI.NewText(root, d.displayName, new Vector2(0, -45), new Vector2(W - 40, 60), 46, d.color,
            TextAlignmentOptions.Center, FontStyles.Bold);

        if (!string.IsNullOrWhiteSpace(d.description))
        {
            var desc = WoodUI.NewText(root, d.description, new Vector2(0, -98), new Vector2(W - 60, 50), 21,
                WoodUI.Cream, TextAlignmentOptions.Center, FontStyles.Italic);
            desc.textWrappingMode = TextWrappingModes.Normal;
        }

        StatRow(root, "Vitesse max", d.maxSpeed / speedRef, $"{d.maxSpeed:0} m/s", -150, d.color);
        StatRow(root, "Vitesse min", d.minSpeed / speedRef, $"{d.minSpeed:0} m/s", -190, d.color);
        StatRow(root, "Allonge", d.maxDraw / drawRef, $"{d.maxDraw * 100f:0} cm", -230, d.color);

        WoodUI.NewImage(root, "Séparateur", new Vector2(0, -262), new Vector2(W - 60, 3), null,
            new Color(0f, 0f, 0f, 0.4f));

        if (d.HasEffect)
        {
            WoodUI.NewText(root, "Effet : " + d.effectName, new Vector2(0, -295), new Vector2(W - 60, 40), 28,
                WoodUI.Gold, TextAlignmentOptions.Center, FontStyles.Bold);
            var fx = WoodUI.NewText(root, d.effectDescription, new Vector2(0, -350), new Vector2(W - 60, 80), 21,
                WoodUI.Cream, TextAlignmentOptions.Top);
            fx.textWrappingMode = TextWrappingModes.Normal;
        }
        else
        {
            WoodUI.NewText(root, "Aucun effet spécial", new Vector2(0, -300), new Vector2(W - 60, 40), 24,
                new Color(1f, 0.94f, 0.8f, 0.6f), TextAlignmentOptions.Center, FontStyles.Italic);
        }
    }

    static void StatRow(Transform parent, string label, float t, string value, float y, Color color)
    {
        const float barLeft = -80f, barW = 250f, barH = 16f;
        WoodUI.NewText(parent, label, new Vector2(-170, y), new Vector2(160, 36), 24, WoodUI.Cream,
            TextAlignmentOptions.Left);
        WoodUI.NewImage(parent, "Barre " + label, new Vector2(barLeft + barW / 2, y), new Vector2(barW, barH), null,
            new Color(0f, 0f, 0f, 0.45f));
        float w = barW * Mathf.Clamp(t, 0.04f, 1f);
        WoodUI.NewImage(parent, "Rempli " + label, new Vector2(barLeft + w / 2, y), new Vector2(w, barH), null, color);
        WoodUI.NewText(parent, value, new Vector2(222, y), new Vector2(85, 36), 22, WoodUI.Cream,
            TextAlignmentOptions.Right);
    }

    /// <summary>Position voulue du panneau (donnée par l'arc à chaque image).</summary>
    public void SetTarget(Vector3 position)
    {
        target = position;
        if (!placed)
        {
            transform.position = position;
            placed = true;
        }
    }

    public void Show(float delay)
    {
        visible = true;
        showAt = Time.time + delay;
        if (!gameObject.activeSelf)
        {
            placed = false;
            content.alpha = 0f;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(true);
        }
    }

    public void Hide() => visible = false;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        bool shown = visible && Time.time >= showAt;

        // Apparition / disparition en douceur
        float k = 1f - Mathf.Exp(-12f * dt);
        transform.localScale = Vector3.Lerp(transform.localScale, shown ? fullScale : Vector3.zero, k);
        content.alpha = Mathf.MoveTowards(content.alpha, shown ? 1f : 0f, dt * 5f);
        if (!visible && content.alpha <= 0.01f)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 bob = Vector3.up * (Mathf.Sin((Time.time + phase) * 1.1f) * 0.01f);
        transform.position = Vector3.Lerp(transform.position, target + bob, 1f - Mathf.Exp(-15f * dt));

        Camera cam = Camera.main;
        if (faceCamera && cam != null)
        {
            Vector3 dir = transform.position - cam.transform.position;   // le canvas regarde dans la même direction que le joueur
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
