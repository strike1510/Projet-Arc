using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fait tomber des pommes du ciel devant le joueur. Une pomme touchée par une flèche éclate et donne des points.
/// À mettre sur un objet vide "PluieDePommes" dans le niveau.
/// "Apple Prefab" : un modèle de pomme du projet. Vide = une boule rouge est créée à la place.
/// </summary>
public class AppleRain : MonoBehaviour
{
    [Header("Pomme")]
    [Tooltip("Prefab de la pomme. Vide = boule rouge.")]
    public GameObject applePrefab;

    [Tooltip("Taille de la pomme (diamètre en m si boule rouge, sinon multiplie la taille du prefab).")]
    public float appleSize = 0.25f;

    [Tooltip("Points gagnés en touchant une pomme.")]
    public int points = 50;

    [Tooltip("Zone touchable autour de la pomme (m).")]
    public float hitRadius = 0.3f;

    [Tooltip("Freinage dans l'air : plus grand = chute plus lente (2 ≈ 5 m/s).")]
    public float airDrag = 2f;

    [Header("Chute")]
    [Tooltip("Temps entre deux pommes (s), au hasard entre ces deux valeurs.")]
    public Vector2 interval = new Vector2(1.5f, 3f);

    [Tooltip("Nombre maximum de pommes en l'air en même temps.")]
    public int maxAlive = 4;

    [Tooltip("Délai avant la première pomme (s).")]
    public float firstDelay = 4f;

    [Tooltip("Distance devant le joueur (m).")]
    public Vector2 distance = new Vector2(5f, 12f);

    [Tooltip("Angle à gauche/droite du regard (degrés).")]
    public float maxAngle = 30f;

    [Tooltip("Hauteur de départ au-dessus du sol (m).")]
    public Vector2 dropHeight = new Vector2(8f, 11f);

    [Tooltip("Temps avant qu'une pomme tombée par terre disparaisse (s).")]
    public float groundLifetime = 3f;

    readonly List<GameObject> alive = new List<GameObject>();
    Camera cam;
    Material redMaterial;

    void Start() => StartCoroutine(Loop());

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            alive.RemoveAll(a => a == null);
            var sm = ScoreManager.Instance;
            if ((sm == null || !sm.IsRoundOver) && alive.Count < maxAlive) DropOne();
            yield return new WaitForSeconds(Random.Range(interval.x, interval.y));
        }
    }

    void DropOne()
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = TargetSpawner.FindGameViewCamera();
        if (cam == null) return;

        Vector3 eye = cam.transform.position;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 dir = Quaternion.AngleAxis(Random.Range(-maxAngle, maxAngle), Vector3.up) * fwd;
        Vector3 pos = eye + dir * Random.Range(distance.x, distance.y);

        float groundY = eye.y - 1.6f;
        if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out RaycastHit g, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            groundY = g.point.y;
        pos.y = groundY + Random.Range(dropHeight.x, dropHeight.y);

        var apple = MakeApple(pos);
        alive.Add(apple);
        StartCoroutine(Life(apple));
    }

    GameObject MakeApple(Vector3 pos)
    {
        GameObject apple;
        if (applePrefab != null)
        {
            apple = Instantiate(applePrefab, pos, Random.rotation);
            apple.transform.localScale = applePrefab.transform.localScale * appleSize / 0.25f;
            if (apple.GetComponentInChildren<Collider>() == null) apple.AddComponent<SphereCollider>();
        }
        else
        {
            // Boule rouge avec une petite queue
            apple = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            apple.transform.position = pos;
            apple.transform.localScale = Vector3.one * appleSize;
            if (redMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                redMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));
                redMaterial.color = new Color(0.8f, 0.08f, 0.05f);
            }
            apple.GetComponent<Renderer>().sharedMaterial = redMaterial;

            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(apple.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            stem.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
            stem.GetComponent<Renderer>().material.color = new Color(0.35f, 0.2f, 0.08f);
        }
        apple.name = "Pomme";

        var rb = apple.GetComponent<Rigidbody>();
        if (rb == null) rb = apple.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.mass = 0.2f;
        rb.linearDamping = airDrag;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var bt = apple.GetComponent<BirdTarget>();
        if (bt == null) bt = apple.AddComponent<BirdTarget>();
        bt.points = points;
        bt.hitRadius = hitRadius;
        bt.reaction = BirdTarget.Reaction.Eclate;
        bt.popupText = "+{0}\n<size=45%>POMME !</size>";
        bt.popupColor = new Color(1f, 0.3f, 0.25f);
        return apple;
    }

    IEnumerator Life(GameObject apple)
    {
        var rb = apple.GetComponent<Rigidbody>();
        float onGround = 0f, total = 0f;
        while (apple != null)
        {
            total += Time.deltaTime;
            // Posée par terre (ou presque immobile) : elle disparaît après quelques secondes
            if (rb != null && !rb.isKinematic && rb.linearVelocity.sqrMagnitude < 0.05f && total > 1f) onGround += Time.deltaTime;
            if (onGround > groundLifetime || total > 30f || apple.transform.position.y < -50f)
            {
                Destroy(apple);
                yield break;
            }
            yield return null;
        }
    }
}
