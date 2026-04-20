using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways, DisallowMultipleComponent]
public class SphereObject : MonoBehaviour
{
    public static readonly List<SphereObject> All = new();

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

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);
}
