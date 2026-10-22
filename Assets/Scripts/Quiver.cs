using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Carquois dans le dos du joueur : on passe la main derrière soi (derrière la tête, par-dessus l'épaule
/// ou dans le dos), on serre le grip, et une flèche apparaît dans la main. On la garde tant que le grip est serré.
/// La flèche donnée est du même type que l'arc tenu dans l'autre main.
///
/// Les flèches du carquois sont de VRAIES flèches de la scène, rangées (cachées) sous ce carquois
/// (Projet Arc > Configurer le projet). Quand elles sont toutes sorties, la plus ancienne tirée revient.
///
/// La flèche est mise en main avec "l'interaction manuelle" de XRI (StartManualInteraction) :
/// la main la tient comme si on l'avait attrapée, et la lâche quand on relâche le grip.
/// </summary>
public class Quiver : MonoBehaviour
{
    [Tooltip("Tête du joueur (Main Camera de l'XR Origin). Vide = trouvée toute seule.")]
    public Transform head;

    [Tooltip("Position du carquois par rapport à la tête (x = droite, y = haut, z = avant), sans tenir compte de l'inclinaison de la tête.")]
    public Vector3 offsetFromHead = new Vector3(0.08f, -0.3f, -0.18f);

    [Tooltip("Distance max entre la main et le carquois pour attraper une flèche (m).")]
    public float grabRadius = 0.55f;

    [Tooltip("La main doit être derrière ce plan (m, par rapport à la tête, vers l'avant). 0.05 = au niveau des oreilles ou derrière.")]
    public float behindHead = 0.05f;

    [Tooltip("Flèches rangées dans le carquois. Vide = ses enfants qui ont ArrowController.")]
    public List<ArrowController> arrows = new List<ArrowController>();

    [Tooltip("Affiche dans la console pourquoi une prise dans le dos est refusée (réglage).")]
    public bool debugLog = false;

    readonly List<XRBaseInputInteractor> hands = new List<XRBaseInputInteractor>();
    readonly Dictionary<ArrowController, float> lastGiven = new Dictionary<ArrowController, float>();
    readonly Dictionary<XRBaseInputInteractor, bool> wasPressed = new Dictionary<XRBaseInputInteractor, bool>();
    readonly HashSet<XRBaseInputInteractor> busy = new HashSet<XRBaseInputInteractor>();
    readonly HashSet<XRBaseInputInteractor> holding = new HashSet<XRBaseInputInteractor>();   // en interaction manuelle

    void Start()
    {
        if (arrows.Count == 0) arrows.AddRange(GetComponentsInChildren<ArrowController>(true));
        foreach (var a in arrows) if (a != null && a.transform.IsChildOf(transform)) a.gameObject.SetActive(false);

        var origin = GetComponentInParent<XROrigin>();
        if (head == null) head = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;

        var root = origin != null ? origin.transform : transform.root;
        foreach (var i in root.GetComponentsInChildren<XRBaseInputInteractor>(true))
            if (i is NearFarInteractor || i is XRDirectInteractor) hands.Add(i);

        if (hands.Count == 0) Debug.LogWarning("[Quiver] Aucune main (Near-Far / Direct Interactor) trouvée sous l'XR Origin.", this);
    }

    Quaternion Yaw()
    {
        Vector3 f = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(head.up, Vector3.up) * -1f;
        return Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    static bool GripHeld(XRBaseInputInteractor hand) => hand.selectInput != null && hand.selectInput.ReadIsPerformed();

    void LateUpdate()
    {
        if (head == null) return;

        // Le carquois suit le dos du joueur
        Quaternion yaw = Yaw();
        transform.SetPositionAndRotation(head.position + yaw * offsetFromHead, yaw);

        foreach (var hand in hands)
        {
            if (hand == null) continue;
            bool pressed = GripHeld(hand);
            bool justPressed = pressed && !(wasPressed.TryGetValue(hand, out bool before) && before);
            wasPressed[hand] = pressed;

            // Grip relâché : la main lâche la flèche donnée (si elle est encochée, elle part)
            if (holding.Contains(hand) && !pressed)
            {
                holding.Remove(hand);
                if (hand.isPerformingManualInteraction) hand.EndManualInteraction();
            }

            if (!justPressed || !hand.isActiveAndEnabled || busy.Contains(hand)) continue;

            Vector3 p = hand.transform.position;
            Vector3 local = Quaternion.Inverse(yaw) * (p - head.position);
            if (local.z > behindHead) { Log(hand, $"main devant la tête (z = {local.z:0.00})"); continue; }
            if (Vector3.Distance(p, transform.position) > grabRadius) { Log(hand, $"main trop loin du carquois ({Vector3.Distance(p, transform.position):0.00} m)"); continue; }
            if (hand.hasSelection) { Log(hand, "la main tient déjà quelque chose"); continue; }

            var arrow = Pick(TypeHeldByOtherHand(hand));
            if (arrow == null) { Log(hand, "aucune flèche libre"); continue; }
            StartCoroutine(Give(hand, arrow));
        }
    }

    void Log(XRBaseInputInteractor hand, string why)
    {
        if (debugLog) Debug.Log($"[Quiver] Pas de flèche pour '{hand.name}' : {why}.", this);
    }

    /// <summary>Type de l'arc tenu par une autre main (null si aucun).</summary>
    static string TypeHeldByOtherHand(XRBaseInputInteractor hand)
    {
        foreach (var bow in BowController.All)
        {
            var g = bow.GetComponent<XRGrabInteractable>();
            if (g == null || !g.isSelected) continue;
            bool sameHand = false;
            foreach (var i in g.interactorsSelecting) if ((Object)i == hand) sameHand = true;
            if (!sameHand) return ArcheryTypes.Of(bow.name);
        }
        return null;
    }

    ArrowController Pick(string type)
    {
        var a = PickOfType(type);
        if (a == null && type != null) a = PickOfType(null);
        return a;
    }

    ArrowController PickOfType(string type)
    {
        // 1) Une flèche encore rangée dans le carquois
        foreach (var a in arrows)
            if (a != null && !a.gameObject.activeSelf && (type == null || ArcheryTypes.Of(a.name) == type)) return a;

        // 2) Sinon la plus anciennement donnée, si personne ne s'en sert
        ArrowController best = null;
        float oldest = float.MaxValue;
        foreach (var a in arrows)
        {
            if (a == null || (type != null && ArcheryTypes.Of(a.name) != type)) continue;
            if (a.IsHeld || a.IsNocked) continue;
            var h = a.GetComponent<ArrowHit>();
            if (h != null && h.IsFlying) continue;
            float t = lastGiven.TryGetValue(a, out float v) ? v : 0f;
            if (t < oldest) { oldest = t; best = a; }
        }
        return best;
    }

    IEnumerator Give(XRBaseInputInteractor hand, ArrowController arrow)
    {
        busy.Add(hand);
        lastGiven[arrow] = Time.time;

        var hit = arrow.GetComponent<ArrowHit>();
        if (hit != null) hit.PrepareForReuse();
        arrow.ResetForReuse();
        arrow.transform.SetParent(null, true);

        // Dans la main : l'encoche sur la main, la pointe vers l'avant de la manette
        Transform hold = hand.transform;
        if (arrow.tip != null && arrow.grabPoint != null)
        {
            Vector3 axis = (arrow.tip.position - arrow.grabPoint.position).normalized;
            arrow.transform.rotation = Quaternion.FromToRotation(axis, hold.forward) * arrow.transform.rotation;
            arrow.transform.position += hold.position - arrow.grabPoint.position;
        }

        var rb = arrow.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        arrow.gameObject.SetActive(true);
        yield return null;   // la flèche s'enregistre auprès de l'Interaction Manager

        var grab = arrow.GetComponent<XRGrabInteractable>();
        if (grab != null && GripHeld(hand) && !hand.hasSelection)
        {
            // La main "attrape" la flèche jusqu'à ce que le grip soit relâché
            hand.StartManualInteraction((IXRSelectInteractable)grab);
            holding.Add(hand);
        }
        else if (debugLog) Debug.Log("[Quiver] Grip relâché avant que la flèche arrive : flèche laissée là.", this);

        busy.Remove(hand);
    }

    void OnDisable()
    {
        foreach (var hand in holding)
            if (hand != null && hand.isPerformingManualInteraction) hand.EndManualInteraction();
        holding.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, grabRadius);
    }
}
