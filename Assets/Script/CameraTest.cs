using UnityEngine;

public class CameraTest : MonoBehaviour
{
    [SerializeField]
    private Vector2Int debugPointCount = new Vector2Int(16, 9);

    [SerializeField]
    private float debugPointRadius = 0.025f;

    [SerializeField]
    private Color pointCol = Color.white;

    private const float ArrowHeadSize = 0.04f;

    [SerializeField]
    private float arrowLength = 0.97f;

    // Thickness not interactive; revert to single-line arrows

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Runtime update no-op; Gizmos drawing happens in OnDrawGizmos
    }

    void CameraRayTest()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Transform camT = cam.transform;

        // Calculate width and height of projection plane
        float planeHeight = cam.nearClipPlane * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;
        // Bottom-left corner of the projection plane (in camera's local space)
        Vector3 bottomLeftLocal = new Vector3(-planeWidth / 2, -planeHeight / 2, cam.nearClipPlane);

        // Draw a grid of points on the plane
        for (int x = 0; x < debugPointCount.x; x++)
        {
            for (int y = 0; y < debugPointCount.y; y++)
            {
                float tx = x / (debugPointCount.x - 1f); // 0 = left edge of plane, 1 = right edge
                float ty = y / (debugPointCount.y - 1f); // 0 = bottom edge of plane, 1 = top edge

                // Calculate point in camera's local space, and then transform to world space
                Vector3 pointLocal = bottomLeftLocal + new Vector3(tx * planeWidth, ty * planeHeight);
                Vector3 point = camT.position + camT.right * pointLocal.x + camT.up * pointLocal.y + camT.forward * pointLocal.z;
                Vector3 dir = (point - camT.position).normalized;

                // Visualize
                DrawPoint(point);
                DrawArrow(camT.position, dir);
            }
        }
    }

    void OnDrawGizmos()
    {
        CameraRayTest();
    }

    void OnValidate()
    {
        // Ensure we have at least a 2x2 grid to avoid division by zero
        debugPointCount.x = Mathf.Max(2, debugPointCount.x);
        debugPointCount.y = Mathf.Max(2, debugPointCount.y);
        // Ensure radius is positive
        debugPointRadius = Mathf.Max(0.0001f, debugPointRadius);
        // Ensure arrow length is positive
        arrowLength = Mathf.Max(0.0001f, arrowLength);
    }

    void DrawPoint(Vector3 position)
    {
        float size = debugPointRadius;
        Gizmos.color = pointCol;
        Gizmos.DrawSphere(position, size);
    }

    void DrawArrow(Vector3 origin, Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-8f) return;
        dir = dir.normalized;

        // Normalized arrow: fixed, configurable length along the ray direction
        Vector3 end = origin + dir * arrowLength;

        Gizmos.color = Color.black;
        // Shaft
        Gizmos.DrawLine(origin, end);

        // Arrowhead using two sides based on a perpendicular vector
        Vector3 perp = Vector3.Cross(dir, Vector3.up);
        if (perp.sqrMagnitude < 1e-8f)
        {
            perp = Vector3.Cross(dir, Vector3.right);
        }
        perp.Normalize();

        Vector3 side1 = (-dir + perp).normalized;
        Vector3 side2 = (-dir - perp).normalized;

        Gizmos.DrawLine(end, end + side1 * ArrowHeadSize);
        Gizmos.DrawLine(end, end + side2 * ArrowHeadSize);
    }
}
