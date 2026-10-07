using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class BowString : MonoBehaviour
{
    public BowController bow;
    public Transform stringTop;
    public Transform stringBottom;

    LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 3;
        line.useWorldSpace = true;
    }

    void LateUpdate()
    {
        Vector3 mid = (bow.nockedArrow != null)
            ? bow.nockedArrow.grabPoint.position  // suit la flèche quand on tire
            : bow.arrowNock.position;             // repos

        line.SetPosition(0, stringTop.position);
        line.SetPosition(1, mid);
        line.SetPosition(2, stringBottom.position);
    }
}