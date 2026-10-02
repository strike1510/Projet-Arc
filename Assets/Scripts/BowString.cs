using UnityEngine;

public class BowString : MonoBehaviour
{
    public Transform stringTop;
    public Transform arrowNock;
    public Transform stringBottom;

    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 3;
    }

    void Update()
    {
        lineRenderer.SetPosition(0, stringTop.position);
        lineRenderer.SetPosition(1, arrowNock.position);
        lineRenderer.SetPosition(2, stringBottom.position);
    }
}