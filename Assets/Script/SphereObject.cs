using UnityEngine;

[DisallowMultipleComponent]
public class SphereObject : MonoBehaviour
{
    [SerializeField]
    private Color color = Color.white;

    [SerializeField]
    private Color emissionColor = Color.black;

    [SerializeField]
    [Min(0)]
    private float emissionStrength = 0f;

    public Color Color => color;
    public Color EmissionColor => emissionColor;
    public float EmissionStrength => emissionStrength;
}
