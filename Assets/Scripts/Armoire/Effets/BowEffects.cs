using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Effets en jeu d'un arc, d'après sa fiche BowData : tir triple, traînée de feuilles, foudre.
/// Ajouté automatiquement par le sélecteur d'arcs (BowSummoner). Peut aussi se mettre à la main sur un arc posé dans la scène.
/// L'effet dépend de l'arc qui tire, pas de la flèche : n'importe quelle flèche ramassée en profite.
/// </summary>
[RequireComponent(typeof(BowController))]
public class BowEffects : MonoBehaviour
{
    public BowData data;

    BowController bow;
    BowString bowString;
    ArrowController lastNocked;

    void Awake()
    {
        bow = GetComponent<BowController>();
        bowString = GetComponentInChildren<BowString>();
    }

    void Update()
    {
        // Tir détecté : une flèche était encochée et ne l'est plus (ArrowController la libère au lâcher)
        var current = bow.nockedArrow;
        if (lastNocked != null && current == null && data != null) StartCoroutine(Fired(lastNocked));
        lastNocked = current;
    }

    IEnumerator Fired(ArrowController arrow)
    {
        // ArrowController donne sa vitesse à la flèche au pas physique suivant : on l'attend
        var rb = arrow.GetComponent<Rigidbody>();
        for (int i = 0; i < 4; i++)
        {
            if (arrow == null || rb == null) yield break;
            if (!rb.isKinematic && rb.linearVelocity.sqrMagnitude > 1f) break;
            yield return new WaitForFixedUpdate();
        }
        if (arrow == null || rb.isKinematic || rb.linearVelocity.sqrMagnitude <= 1f) yield break;

        Vector3 velocity = rb.linearVelocity;

        // Les flèches bonus sont copiées AVANT d'ajouter les effets à la flèche principale
        if (data.tripleShot)
        {
            Vector3 axis = SpreadAxis(velocity);
            SpawnBonus(arrow, Quaternion.AngleAxis(-data.tripleSpread, axis) * velocity, axis, -1f);
            SpawnBonus(arrow, Quaternion.AngleAxis(data.tripleSpread, axis) * velocity, axis, 1f);
        }

        AddFlightEffects(arrow.gameObject, arrow.tip, arrow.grabPoint);
    }

    // Axe de la corde : les flèches bonus s'écartent à gauche et à droite de l'arc
    Vector3 SpreadAxis(Vector3 velocity)
    {
        Vector3 a = Vector3.zero;
        if (bowString != null && bowString.stringTop != null && bowString.stringBottom != null)
            a = Vector3.ProjectOnPlane(bowString.stringTop.position - bowString.stringBottom.position, velocity);
        if (a.sqrMagnitude < 1e-4f) a = Vector3.ProjectOnPlane(Vector3.up, velocity);
        return a.sqrMagnitude < 1e-4f ? Vector3.up : a.normalized;
    }

    void SpawnBonus(ArrowController arrow, Vector3 velocity, Vector3 axis, float sign)
    {
        Vector3 side = Vector3.Cross(velocity.normalized, axis).normalized * (sign * 0.06f);
        var go = Instantiate(arrow.gameObject, arrow.transform.position + side, arrow.transform.rotation);
        go.name = arrow.name + " (bonus)";

        var copy = go.GetComponent<ArrowController>();
        Transform tip = copy.tip, back = copy.grabPoint;

        // La copie n'est qu'une flèche décorative qui vole toute seule : on retire tout le reste
        if (go.TryGetComponent(out ArrowHit hit)) DestroyImmediate(hit);
        DestroyImmediate(copy);
        if (go.TryGetComponent(out XRGrabInteractable grab)) DestroyImmediate(grab);
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        if (go.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Vector3 arrowAxis = (tip.position - back.position).normalized;
        go.transform.rotation = Quaternion.FromToRotation(arrowAxis, velocity.normalized) * go.transform.rotation;

        go.AddComponent<BonusArrow>().Launch(tip, back, velocity, data.bonusPointsFactor, transform);
        AddFlightEffects(go, tip, back);
    }

    void AddFlightEffects(GameObject arrow, Transform tip, Transform back)
    {
        if (data.leafTrail) arrow.AddComponent<LeafTrail>().Init(tip, back);
        if (data.lightning) arrow.AddComponent<LightningArrow>().Init(tip, back, data.burnDuration);
    }
}
