using UnityEngine;

public class BowController : MonoBehaviour
{
    public Transform arrowNock;   // Z (bleu) = direction du tir
    public float nockDistance = 0.15f;
    public float maxDraw = 0.5f;
    public float minSpeed = 5f;
    public float maxSpeed = 30f;

    [HideInInspector] public ArrowController nockedArrow; // flèche encochée, null si aucune
}