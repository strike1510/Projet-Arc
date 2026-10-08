using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Animation de survol pour un bouton de menu : marche avec le rayon des manettes VR
/// (XR UI Input Module) comme avec la souris.
/// Quand on vise le bouton, il grossit légèrement et avance vers le joueur.
/// Option : une flèche vient se planter dans le bouton et vibre.
/// À placer sur le bouton lui-même (le GameObject qui a le composant Button).
/// </summary>
[RequireComponent(typeof(Selectable))]
public class BoutonSurvol : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Survol")]
    [Tooltip("Taille du bouton quand il est visé (1 = taille normale).")]
    [SerializeField] float echelleSurvol = 1.08f;
    [Tooltip("Distance dont le bouton avance vers le joueur, en unités du Canvas " +
             "(avec un Canvas à l'échelle 0.001, 30 = 3 cm).")]
    [SerializeField] float avanceSurvol = 30f;
    [Tooltip("Durée de l'animation, en secondes.")]
    [SerializeField] float duree = 0.15f;

    [Header("Flèche plantée (optionnel)")]
    [Tooltip("Image de flèche, enfant du bouton, avec le pivot sur la pointe (Pivot X = 1). " +
             "Placez-la dans l'éditeur à sa position finale. Laisser vide pour ne pas l'utiliser.")]
    [SerializeField] RectTransform fleche;
    [Tooltip("Distance parcourue par la flèche avant l'impact, en unités du Canvas.")]
    [SerializeField] float distanceVol = 250f;
    [SerializeField] float dureeVol = 0.2f;
    [Tooltip("Angle maximal de la vibration après l'impact, en degrés.")]
    [SerializeField] float angleVibration = 9f;

    [Header("Sons (optionnel)")]
    [SerializeField] AudioSource sourceAudio;
    [SerializeField] AudioClip sonSurvol;
    [SerializeField] AudioClip sonImpact;
    [SerializeField] AudioClip sonClic;

    RectTransform rt;
    Selectable selectable;
    Vector3 echelleBase;
    float zBase;
    Vector2 posFleche;
    int pointeurs;              // nombre de rayons (ou souris) qui visent le bouton
    Coroutine animBouton;
    Coroutine animFleche;

    void Awake()
    {
        rt = (RectTransform)transform;
        selectable = GetComponent<Selectable>();
        echelleBase = rt.localScale;
        zBase = rt.anchoredPosition3D.z;

        // Seul le fond du bouton doit capter le rayon : le texte et la flèche, non.
        // Sinon le survol peut « clignoter » quand le rayon passe sur le texte.
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
            if (g.gameObject != gameObject) g.raycastTarget = false;

        if (fleche != null)
        {
            posFleche = fleche.anchoredPosition;
            fleche.gameObject.SetActive(false);
        }
    }

    void OnDisable()
    {
        // Remise à zéro si le panneau est masqué pendant le survol (ex. clic sur « Options »).
        StopAllCoroutines();
        animBouton = null;
        animFleche = null;
        pointeurs = 0;
        rt.localScale = echelleBase;
        DefinirZ(zBase);
        if (fleche != null)
        {
            fleche.localRotation = Quaternion.identity;
            fleche.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!selectable.IsInteractable()) return;
        pointeurs++;
        if (pointeurs > 1) return;      // déjà visé par une autre manette

        AnimerBouton(echelleSurvol, zBase - avanceSurvol);
        JouerSon(sonSurvol);

        if (fleche != null)
        {
            if (animFleche != null) StopCoroutine(animFleche);
            animFleche = StartCoroutine(TirerFleche());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointeurs = Mathf.Max(0, pointeurs - 1);
        if (pointeurs > 0) return;      // une autre manette vise encore le bouton

        AnimerBouton(1f, zBase);

        if (fleche != null)
        {
            if (animFleche != null) StopCoroutine(animFleche);
            animFleche = null;
            fleche.localRotation = Quaternion.identity;
            fleche.gameObject.SetActive(false);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!selectable.IsInteractable()) return;
        AnimerBouton(echelleSurvol * 0.96f, zBase - avanceSurvol * 0.3f);   // le bouton s'enfonce
        JouerSon(sonClic);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pointeurs > 0) AnimerBouton(echelleSurvol, zBase - avanceSurvol);
        else AnimerBouton(1f, zBase);
    }

    // ---------------------------------------------------------------------

    void AnimerBouton(float facteur, float zCible)
    {
        if (!isActiveAndEnabled) return;
        if (animBouton != null) StopCoroutine(animBouton);
        animBouton = StartCoroutine(Interpoler(echelleBase * facteur, zCible));
    }

    IEnumerator Interpoler(Vector3 echelleCible, float zCible)
    {
        Vector3 echelleDepart = rt.localScale;
        float zDepart = rt.anchoredPosition3D.z;
        float d = Mathf.Max(duree, 0.01f);

        // Time.unscaledDeltaTime : l'animation marche même si le jeu est en pause (timeScale = 0).
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / d)
        {
            float k = 1f - (1f - t) * (1f - t);      // démarre vite, ralentit à la fin
            rt.localScale = Vector3.LerpUnclamped(echelleDepart, echelleCible, k);
            DefinirZ(Mathf.LerpUnclamped(zDepart, zCible, k));
            yield return null;
        }

        rt.localScale = echelleCible;
        DefinirZ(zCible);
        animBouton = null;
    }

    IEnumerator TirerFleche()
    {
        fleche.gameObject.SetActive(true);
        fleche.localRotation = Quaternion.identity;
        Vector2 depart = posFleche + Vector2.left * distanceVol;
        float d = Mathf.Max(dureeVol, 0.01f);

        // 1. Vol : la flèche accélère jusqu'à l'impact.
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / d)
        {
            fleche.anchoredPosition = Vector2.LerpUnclamped(depart, posFleche, t * t);
            yield return null;
        }
        fleche.anchoredPosition = posFleche;
        JouerSon(sonImpact);

        // 2. Vibration amortie autour de la pointe (d'où le pivot X = 1).
        const float dureeVibration = 0.7f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / dureeVibration)
        {
            float amorti = (1f - t) * (1f - t);
            float angle = -angleVibration * Mathf.Sin(t * 30f) * amorti;
            fleche.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        fleche.localRotation = Quaternion.identity;
        animFleche = null;
    }

    void JouerSon(AudioClip clip)
    {
        if (sourceAudio != null && clip != null) sourceAudio.PlayOneShot(clip);
    }

    void DefinirZ(float z)
    {
        Vector3 p = rt.anchoredPosition3D;
        p.z = z;
        rt.anchoredPosition3D = p;
    }
}
