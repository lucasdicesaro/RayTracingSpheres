using UnityEngine;
using System.Runtime.InteropServices;
[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour
{
    [Header("Ray Tracing Settings")]
    [SerializeField, Min(0)] int MaxBounceCount = 30;
    [SerializeField, Min(1)] int NumRaysPerPixel = 10;
    [SerializeField] bool Accumulate = true;
    [SerializeField, InspectorName("Update In Edit Mode")] bool updateInEditMode = false;
    [SerializeField, Min(0), InspectorName("Num Rendered Frames")] int frameIndex;

    [Header("View Settings")]
    [SerializeField, InspectorName("Use Shader in Scene View")] bool useShaderInSceneView = true;

    [Header("References")]
    [SerializeField] Shader rayTracingShader;
    [SerializeField] Shader comparisonShader; // assign Custom/Comparison

    Material rayTracingMaterial;
    Material comparisonMaterial;
    ComputeBuffer sphereBuffer;
    Sphere[] sphereData;

    // Accumulation ping-pong
    RenderTexture accumA; // write target
    RenderTexture accumB; // read source

    [StructLayout(LayoutKind.Sequential)]
    struct RayTracingMaterial
    {
        public Vector4 color;           // RGBA
        public Vector3 emissionColor;   // RGB
        public float emissionStrength;  // scalar
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Sphere
    {
        public Vector3 position;
        public float radius;
        public RayTracingMaterial material;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Application.isPlaying || updateInEditMode)
        {
            if (Accumulate) { frameIndex++; } else { frameIndex = Time.frameCount; }
        }
        else
        {
            frameIndex = 0;
        }
    }

    // Called after each camera (e.g. game or scene camera) has finished rendering into the src texture
    void OnRenderImage(RenderTexture src, RenderTexture target)
    {
        if (Camera.current.name != "SceneCamera" || useShaderInSceneView)
        {
            ShaderHelper.InitMaterial(rayTracingShader, ref rayTracingMaterial);
            if (rayTracingMaterial == null)
            {
                Graphics.Blit(src, target); // shader not set; bail out safely
                return;
            }

            // Only create/use comparison material when accumulating
            if (Accumulate && comparisonMaterial == null && comparisonShader != null)
            {
                ShaderHelper.InitMaterial(comparisonShader, ref comparisonMaterial);
            }

            UpdateCameraParams(Camera.current);
            SendSphereParams();
            SendEnvironmentParams();

            rayTracingMaterial.SetInt("MaxBounceCount", MaxBounceCount);
            rayTracingMaterial.SetInt("NumRaysPerPixel", NumRaysPerPixel);
            rayTracingMaterial.SetInt("Frame", frameIndex);

            if (!Accumulate)
            {
                Graphics.Blit(null, target, rayTracingMaterial);
                return;
            }

            // Ensure accumulation buffers (float to avoid clamping HDR values)
            var desc = src.descriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.colorFormat = RenderTextureFormat.ARGBFloat;
            desc.sRGB = false;
            if (accumA == null || accumA.width != src.width || accumA.height != src.height)
            {
                if (accumA) { accumA.Release(); accumA = null; }
                if (accumB) { accumB.Release(); accumB = null; }
                accumA = new RenderTexture(desc) { filterMode = FilterMode.Point }; accumA.Create();
                accumB = new RenderTexture(desc) { filterMode = FilterMode.Point }; accumB.Create();
                frameIndex = 0;
            }

            // Render current frame into a float RT to avoid clamping HDR values
            var rtDesc = src.descriptor;
            rtDesc.colorFormat = RenderTextureFormat.ARGBFloat;
            rtDesc.sRGB = false;
            var currentRT = RenderTexture.GetTemporary(rtDesc);
            Graphics.Blit(null, currentRT, rayTracingMaterial);

            // Set frame count and explicitly bind Comparison shader inputs
            int previousCount = Mathf.Max(0, frameIndex - 1);
            comparisonMaterial.SetInt("NumRenderedFrames", previousCount);
            comparisonMaterial.SetTexture("_MainTex", currentRT);    // new render
            comparisonMaterial.SetTexture("_MainTexOld", accumB);    // previous accumulated

            // Blend into write target
            Graphics.Blit(null, accumA, comparisonMaterial);

            // Ping-pong and output
            var tmp = accumB; accumB = accumA; accumA = tmp;
            Graphics.Blit(accumB, target);
            RenderTexture.ReleaseTemporary(currentRT);
        }
        else
        {
            Graphics.Blit(src, target);
        }
    }

    void UpdateCameraParams(Camera cam)
    {
        float planeHeight = cam.nearClipPlane * Mathf.Tan(0.5f * cam.fieldOfView * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * cam.aspect;
        // Send data to shader
        rayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, cam.nearClipPlane));
        rayTracingMaterial.SetMatrix("CamLocalToWorldMatrix", cam.transform.localToWorldMatrix);
    }

    void SendSphereParams()
    {
        var sphereObjects = SphereObject.All;
        int count = sphereObjects.Count;

        // Prepare data
        if (sphereData == null || sphereData.Length != count)
            sphereData = new Sphere[count];
        for (int i = 0; i < count; i++)
        {
            var s = sphereObjects[i];
            sphereData[i].position = s.transform.position;
            // Derive radius from scale (user edits scale in Scene view)
            sphereData[i].radius = 0.5f * Mathf.Max(s.transform.lossyScale.x, s.transform.lossyScale.y, s.transform.lossyScale.z);
            var baseCol = s.Color;
            var emitCol = s.EmissionColor;
            sphereData[i].material.color = new Vector4(baseCol.r, baseCol.g, baseCol.b, baseCol.a);
            sphereData[i].material.emissionColor = new Vector3(emitCol.r, emitCol.g, emitCol.b);
            sphereData[i].material.emissionStrength = s.EmissionStrength;
        }

        // Create or resize buffer
        int stride = sizeof(float) * (3 + 1 + 4 + 3 + 1); // 48 bytes: pos(3) + radius(1) + color(4) + emissionColor(3) + emissionStrenght(1)
        if (sphereBuffer == null || sphereBuffer.count != Mathf.Max(1, count))
        {
            sphereBuffer?.Release();
            sphereBuffer = new ComputeBuffer(Mathf.Max(1, count), stride, ComputeBufferType.Structured);
        }
        if (count > 0)
        {
            sphereBuffer.SetData(sphereData);
        }

        // Bind to material
        rayTracingMaterial.SetInt("NumSpheres", count);
        rayTracingMaterial.SetBuffer("Spheres", sphereBuffer);
    }

    void SendEnvironmentParams()
    {
        // Sky and ground colors
        var zenith = new Color(0.15f, 0.2f, 0.4f, 1f);
        var horizon = new Color(0.5f, 0.6f, 0.8f, 1f);
        var ground = new Color(0.2f, 0.18f, 0.15f, 1f);
        rayTracingMaterial.SetVector("SkyColorZenith", new Vector4(zenith.r, zenith.g, zenith.b, 0f));
        rayTracingMaterial.SetVector("SkyColorHorizon", new Vector4(horizon.r, horizon.g, horizon.b, 0f));
        rayTracingMaterial.SetVector("GroundColor", new Vector4(ground.r, ground.g, ground.b, 0f));

        // Sun direction and intensity
        Light dirLight = RenderSettings.sun;
        Vector3 sunDir = dirLight != null ? dirLight.transform.forward : Vector3.down;
        float sunIntensity = dirLight != null ? dirLight.intensity : 0f;
        float sunFocus = 64f;
        rayTracingMaterial.SetVector("SunLightDirection", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
        rayTracingMaterial.SetFloat("SunIntensity", sunIntensity);
        rayTracingMaterial.SetFloat("SunFocus", sunFocus);
    }

    void OnDisable()
    {
        // Safely release ping-pong RTs
        if (accumA != null)
        {
            if (accumA.IsCreated()) accumA.Release();
#if UNITY_EDITOR
            Object.DestroyImmediate(accumA);
#else
            Object.Destroy(accumA);
#endif
            accumA = null;
        }

        if (accumB != null)
        {
            if (accumB.IsCreated()) accumB.Release();
#if UNITY_EDITOR
            Object.DestroyImmediate(accumB);
#else
            Object.Destroy(accumB);
#endif
            accumB = null;
        }

        sphereBuffer?.Release(); 
        sphereBuffer = null;
    }
}
