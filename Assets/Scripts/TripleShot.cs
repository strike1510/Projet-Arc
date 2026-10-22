using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tir triple (Bow_Monkey) : quand une flèche part de cet arc, deux flèches latérales partent avec elle,
/// une un peu à gauche et une un peu à droite.
/// Les deux flèches latérales sont de VRAIES flèches posées dans la scène (enfants "Flèche gauche" et
/// "Flèche droite" de l'arc, cachées au départ). Après le tir elles restent où elles sont tombées
/// (on peut même les ramasser) et repartent au tir suivant.
/// Leurs points comptent, mais elles ne consomment pas de flèche de la manche.
/// </summary>
[RequireComponent(typeof(BowController))]
public class TripleShot : MonoBehaviour
{
    [Tooltip("Flèche qui part à gauche.")]
    public ArrowHit leftArrow;
    [Tooltip("Flèche qui part à droite.")]
    public ArrowHit rightArrow;

    [Tooltip("Écart des flèches latérales (degrés).")]
    public float spreadAngle = 7f;
    [Tooltip("Décalage sur le côté au départ (m), pour que les flèches ne se touchent pas.")]
    public float sideOffset = 0.05f;
    [Tooltip("Vitesse des flèches latérales par rapport à la flèche du centre.")]
    public float speedFactor = 0.97f;

    BowController bow;

    void Awake() => bow = GetComponent<BowController>();
    void OnEnable() => bow.ArrowFired += OnArrowFired;
    void OnDisable() => bow.ArrowFired -= OnArrowFired;

    void OnArrowFired(ArrowController main, Vector3 dir, float speed, Vector3 origin)
    {
        // Les flèches s'écartent dans le plan de l'arc (arc tenu droit = gauche / droite)
        Vector3 up = bow.arrowNock != null ? bow.arrowNock.up : transform.up;
        if (!bow.IsHeld) up = Vector3.up;   // arc posé (tir de test) : gauche / droite du monde
        up = Vector3.ProjectOnPlane(up, dir);
        if (up.sqrMagnitude < 1e-4f) up = Vector3.ProjectOnPlane(Vector3.up, dir);
        up.Normalize();
        Vector3 right = Vector3.Cross(up, dir).normalized;

        var group = new List<GameObject> { main.gameObject };
        if (leftArrow != null) group.Add(leftArrow.gameObject);
        if (rightArrow != null) group.Add(rightArrow.gameObject);

        StartCoroutine(Launch(main, leftArrow, Quaternion.AngleAxis(-spreadAngle, up) * dir, origin - right * sideOffset, speed, group));
        StartCoroutine(Launch(main, rightArrow, Quaternion.AngleAxis(spreadAngle, up) * dir, origin + right * sideOffset, speed, group));
    }

    IEnumerator Launch(ArrowController main, ArrowHit side, Vector3 dir, Vector3 origin, float speed, List<GameObject> group)
    {
        if (side == null || side.gameObject == main.gameObject) yield break;

        var sideArrow = side.GetComponent<ArrowController>();
        if (sideArrow != null && (sideArrow.IsHeld || sideArrow.IsNocked)) yield break;   // le joueur s'en sert

        side.transform.SetParent(null, true);
        if (!side.gameObject.activeSelf) side.gameObject.SetActive(true);

        // Les trois flèches et l'arc ne se gênent pas
        IgnoreBetween(side.gameObject, group);
        IgnoreBetween(side.gameObject, new List<GameObject> { gameObject });

        yield return new WaitForFixedUpdate();   // même moment que la flèche du centre (ArrowController.Fire)
        side.LaunchBonus(origin, dir, speed * speedFactor, bow);
    }

    static void IgnoreBetween(GameObject a, List<GameObject> others)
    {
        var ca = a.GetComponentsInChildren<Collider>();
        foreach (var o in others)
        {
            if (o == null || o == a) continue;
            foreach (var cb in o.GetComponentsInChildren<Collider>())
                foreach (var c in ca)
                    if (c != null && cb != null) Physics.IgnoreCollision(c, cb, true);
        }
    }
}
