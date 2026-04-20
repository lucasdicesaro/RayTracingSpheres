Shader "Hidden/RayTracingSpheres"
{
    Properties {}

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Parameters set from RayTracingManager
            float4 ViewParams;              // (planeWidth, planeHeight, nearClip, 0)
            float4x4 CamLocalToWorldMatrix; // camera transform

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            struct Ray {
                float3 origin;
                float3 dir;
            };

            struct HitInfo {
                bool didHit;
                float dst;
                float3 hitPoint;
                float3 normal;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Calculate the intersection between a ray and a sphere
            HitInfo RaySphere(Ray ray, float3 sphereCenter, float sphereRadius)
            {
                HitInfo hitInfo = (HitInfo)0;

                float3 offsetRayOrigin = ray.origin - sphereCenter;
                // From the equation: sqrLength(rayOrigin + rayDir * dst) = radius^2
                // Solving for dst results in a quadratic equation with coefficients:
                float a = dot(ray.dir, ray.dir); // a = 1 (assuming unit vector)
                float b = 2.0 * dot(offsetRayOrigin, ray.dir);
                float c = dot(offsetRayOrigin, offsetRayOrigin) - sphereRadius * sphereRadius;
                // Quadratic discriminant
                float discriminant = b * b - 4 * a * c;

                // No solution when d < 0 (ray misses sphere)
                if (discriminant >= 0)
                {
                    // Distance to nearest intersection point (from quadratic formula)
                    float dst = (-b - sqrt(discriminant)) / (2.0 * a);

                    // Ignore intersections behind the ray origin
                    if (dst > 0)
                    {
                        hitInfo.didHit = true;
                        hitInfo.dst = dst;
                        hitInfo.hitPoint = ray.origin + dst * ray.dir;
                        hitInfo.normal = normalize(hitInfo.hitPoint - sphereCenter);
                    }
                }

                return hitInfo;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));

                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);
                return RaySphere(ray, 0, 1).didHit;
            }
            ENDCG
        }
    }
}
