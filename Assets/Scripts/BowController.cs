using System;
using System.Collections.Generic;
using UnityEngine;

public class BowController : MonoBehaviour
{
    public static readonly List<BowController> All = new List<BowController>();

    public Transform arrowNock;   // Z (bleu) = direction du tir
    public float nockDistance = 0.15f;
    public float maxDraw = 0.5f;
    public float minSpeed = 5f;
    public float maxSpeed = 30f;

    [Header("Traînée des flèches tirées par cet arc")]
    [Tooltip("Couleurs de la traînée, du départ (près de la flèche) à la fin.")]
    public Gradient trailColors;
    [Tooltip("Largeur de la traînée au départ (m).")]
    public float trailWidth = 0.04f;
    [Tooltip("Durée de vie de la traînée (s) : plus long = traînée plus longue.")]
    public float trailTime = 0.4f;
    [Tooltip("La traînée scintille (effet électrique).")]
    public bool trailFlicker = false;

    [HideInInspector] public ArrowController nockedArrow; // flèche encochée, null si aucune

    /// <summary>Une flèche vient d'être tirée par cet arc : (flèche, direction, vitesse, position de l'encoche de la flèche au départ).</summary>
    public event Action<ArrowController, Vector3, float, Vector3> ArrowFired;

    public void NotifyFired(ArrowController arrow, Vector3 dir, float speed, Vector3 origin)
        => ArrowFired?.Invoke(arrow, dir, speed, origin);

    /// <summary>Vrai si une main tient cet arc.</summary>
    public bool IsHeld
    {
        get
        {
            var g = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            return g != null && g.isSelected;
        }
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
}
