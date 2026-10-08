using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu de démarrage : lancer le jeu, afficher les options, quitter.
/// Les méthodes publiques se branchent sur l'événement « On Click () » de chaque bouton.
/// </summary>
public class MenuDemarrage : MonoBehaviour
{
    [Tooltip("Nom exact de la scène de jeu. Elle doit être dans File > Build Profiles > Scene List.")]
    [SerializeField] string sceneDeJeu = "SampleScene";

    [Header("Panneaux du menu")]
    [SerializeField] GameObject panneauPrincipal;
    [SerializeField] GameObject panneauOptions;

    bool chargementEnCours;

    void Start()
    {
        AfficherPrincipal();
    }

    // Bouton « Jouer »
    public void Jouer()
    {
        if (chargementEnCours) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneDeJeu))
        {
            Debug.LogError($"La scène \"{sceneDeJeu}\" n'est pas dans la liste des scènes " +
                           "(File > Build Profiles > Scene List).");
            return;
        }

        chargementEnCours = true;
        // Chargement asynchrone : l'image ne se fige pas dans le casque pendant le chargement.
        SceneManager.LoadSceneAsync(sceneDeJeu);
    }

    // Bouton « Options »
    public void AfficherOptions()
    {
        if (panneauPrincipal != null) panneauPrincipal.SetActive(false);
        if (panneauOptions != null) panneauOptions.SetActive(true);
    }

    // Bouton « Retour » du panneau Options
    public void AfficherPrincipal()
    {
        if (panneauPrincipal != null) panneauPrincipal.SetActive(true);
        if (panneauOptions != null) panneauOptions.SetActive(false);
    }

    // Bouton « Quitter »
    public void Quitter()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // dans l'éditeur : arrête le mode Play
#else
        Application.Quit();
#endif
    }
}
