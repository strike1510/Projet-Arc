using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pancarte en bois posée au sol (sur pieds, comme une cible) qui affiche la manche en cours :
/// numéro de manche, score / objectif, une pastille par flèche, flèches restantes et la légende des couleurs.
/// Usage : un GameObject vide placé au sol, à côté du joueur + ce script.
/// La pancarte se tourne vers le joueur au lancement.
/// </summary>
public class ArrowHUD : MonoBehaviour
{
    [Header("Pancarte")]
    [Tooltip("Largeur de la pancarte en mètres.")]
    public float width = 1.2f;

    [Tooltip("Hauteur du centre de la pancarte au-dessus du sol (m).")]
    public float signHeight = 1.15f;

    [Tooltip("Inclinaison vers l'arrière, en degrés (comme la cible sur trépied).")]
    public float tiltDegrees = 12f;

    [Tooltip("Pose automatiquement les pieds sur le terrain au lancement.")]
    public bool snapToGround = true;

    [Tooltip("Se tourne vers le joueur au lancement. Sinon, garde la rotation de l'objet.")]
    public bool facePlayerAtStart = true;

    [Header("Affichage")]
    [Tooltip("Cache la pancarte quand la manche est finie (le tableau des scores prend le relais).")]
    public bool hideWhenRoundOver = false;

    // Canvas en unités (W x H), mis à l'échelle pour faire 'width' mètres de large
    const float W = 520f, H = 270f, Pad = 20f;

    Transform sign;
    RectTransform content;
    Camera cam;
    float punch;
    int lastShotCount = -1;
    float PixelToMeter => width / W;

    void Start()
    {
        cam = Camera.main;

        if (snapToGround &&
            Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 50f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            transform.position = hit.point;

        if (facePlayerAtStart && cam != null)
        {
            Vector3 dir = transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
        }

        Build();

        var sm = ScoreManager.Instance;
        if (sm == null)
        {
            Debug.LogError("[ArrowHUD] Pas de ScoreManager dans la scène.");
            return;
        }
        sm.OnScoreChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null) ScoreManager.Instance.OnScoreChanged -= Refresh;
    }

    void Update()
    {
        // Petit rebond de la pancarte quand une flèche est comptée
        if (sign == null) return;
        punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 3f);
        sign.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(punch * Mathf.PI));
    }

    // ---------- Construction ----------

    void Build()
    {
        var wood = WoodUI.MakeWoodMaterial();
        float wm = W * PixelToMeter, hm = H * PixelToMeter;

        // Pancarte (inclinée vers l'arrière)
        sign = new GameObject("Pancarte").transform;
        sign.SetParent(transform, false);
        sign.localPosition = Vector3.up * signHeight;
        sign.localRotation = Quaternion.Euler(tiltDegrees, 0f, 0f);   // le haut part vers l'arrière

        Cube("Planche", sign, new Vector3(0f, 0f, 0.025f), new Vector3(wm + 0.06f, hm + 0.06f, 0.04f), Quaternion.identity, wood);

        // Deux pieds devant + une jambe de force derrière
        float legH = signHeight - hm / 2f + 0.1f;
        float legX = wm / 2f - 0.12f;
        Cube("Pied gauche", transform, new Vector3(-legX, legH / 2f, 0.06f), new Vector3(0.05f, legH, 0.05f), Quaternion.identity, wood);
        Cube("Pied droit", transform, new Vector3(legX, legH / 2f, 0.06f), new Vector3(0.05f, legH, 0.05f), Quaternion.identity, wood);
        // Jambe de force : du dos de la pancarte jusqu'au sol, 25° vers l'arrière
        const float strutAngle = 25f;
        float a = strutAngle * Mathf.Deg2Rad;
        float strutLength = signHeight / Mathf.Cos(a);
        Vector3 strutTop = new Vector3(0f, signHeight, 0.08f);
        Vector3 strutDir = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
        Cube("Jambe arrière", transform, strutTop - strutDir * (strutLength / 2f), new Vector3(0.05f, strutLength, 0.05f),
            Quaternion.Euler(-strutAngle, 0f, 0f), wood);

        // Canvas (face avant tournée vers le joueur)
        var canvasGO = new GameObject("PancarteCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(sign, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        var rt = (RectTransform)canvasGO.transform;
        rt.sizeDelta = new Vector2(W, H);
        rt.localScale = Vector3.one * PixelToMeter;

        var bg = WoodUI.NewImage(rt, "Fond", Vector2.zero, Vector2.zero,
            WoodUI.MakeSprite(WoodUI.MakeWoodTexture(512, 266, 3, 3f, true)), Color.white);
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.sizeDelta = Vector2.zero;

        content = WoodUI.NewRect(rt, "Contenu", Vector2.zero, Vector2.zero);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.sizeDelta = Vector2.zero;
    }

    static void Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());   // les flèches ne s'y plantent pas
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // ---------- Contenu ----------

    void Refresh()
    {
        var sm = ScoreManager.Instance;
        if (sm == null || content == null) return;

        sign.gameObject.SetActive(!(hideWhenRoundOver && sm.IsRoundOver));

        int shots = sm.ShotsOfRound(sm.RoundNumber - 1).Count;
        if (lastShotCount >= 0 && shots > lastShotCount) punch = 1f;
        lastShotCount = shots;

        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);

        const float inner = W - 2 * Pad;

        // Manche + score
        WoodUI.NewText(content, $"Manche {sm.RoundNumber}", new Vector2(0, -Pad - 26), new Vector2(inner, 52), 38,
            WoodUI.Cream, TextAlignmentOptions.Left, FontStyles.Bold);
        WoodUI.NewText(content, $"{sm.RoundScore} / {sm.targetScore}", new Vector2(0, -Pad - 26), new Vector2(inner, 52), 44,
            sm.TargetReached ? WoodUI.Green : WoodUI.Gold, TextAlignmentOptions.Right, FontStyles.Bold);

        // Une pastille par flèche
        WoodUI.ShotDots(content, sm.ShotsOfRound(sm.RoundNumber - 1), sm.arrowsPerRound, -Pad - 90, 38f, 9f);

        // Flèches restantes
        string left = sm.IsRoundOver ? (sm.TargetReached ? "Objectif atteint !" : "Manche terminée")
                    : sm.ArrowsLeft > 1 ? $"{sm.ArrowsLeft} flèches restantes"
                    : sm.ArrowsLeft == 1 ? "Dernière flèche !" : "Plus de flèches";
        WoodUI.NewText(content, left, new Vector2(0, -Pad - 145), new Vector2(inner, 44), 34,
            sm.IsRoundOver || sm.ArrowsLeft <= 1 ? WoodUI.Gold : WoodUI.Cream);

        // Légende des couleurs
        WoodUI.NewImage(content, "Fond légende", new Vector2(0, -Pad - 205), new Vector2(inner, 50), null,
            new Color(0.15f, 0.08f, 0.03f, 0.55f));
        WoodUI.ShotLegend(content, -Pad - 205, inner - 16, 28f, 28f);
    }
}
