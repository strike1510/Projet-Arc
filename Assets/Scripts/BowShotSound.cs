using UnityEngine;

// Joue un son quand la flèche encochée est relâchée.
[RequireComponent(typeof(BowController))]
public class BowShotSound : MonoBehaviour
{
    public AudioClip shotSound;
    [Range(0f, 1f)] public float volume = 1f;

    BowController bow;
    bool wasNocked;

    void Awake()
    {
        bow = GetComponent<BowController>();
    }

    void Update()
    {
        bool nocked = bow.nockedArrow != null;

        // Flèche encochée à la frame précédente, plus maintenant : elle vient d'être lâchée
        if (wasNocked && !nocked && shotSound != null)
            AudioSource.PlayClipAtPoint(shotSound, bow.arrowNock.position, volume);

        wasNocked = nocked;
    }
}