using TMPro;
using UnityEngine;

/// <summary>
/// Une ligne du panneau des résultats : "Manche 1 ..... 46 pts" + une rangée de ronds (un par flèche).
/// À mettre sur la racine du prefab de ligne. ResultsBoard la remplit.
/// </summary>
public class RoundRow : MonoBehaviour
{
    [Tooltip("Texte du nom de la manche (\"Manche 1\").")]
    public TMP_Text roundLabel;

    [Tooltip("Texte du score de la manche (\"46 pts\").")]
    public TMP_Text scoreLabel;

    [Tooltip("Objet qui contient les ronds (avec un Horizontal Layout Group).")]
    public Transform dotsContainer;
}
