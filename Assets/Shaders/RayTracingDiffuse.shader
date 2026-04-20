Shader "Custom/RayTracingDiffuse"
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

            struct RayTracingMaterial {
                float4 color;
                float3 emissionColor;
                float emissionStrength;
            };

            struct HitInfo {
                bool didHit;
                float dst;
                float3 hitPoint;
                float3 normal;
                RayTracingMaterial material;
            };

            struct Sphere {
                float3 position;
                float radius;
                RayTracingMaterial material;
            };

            struct Environment {
                float4 color;
                float3 emissionColor;
                float emissionStrength;
            };

            StructuredBuffer<Sphere> Spheres;
            int NumSpheres;
            int MaxBounceCount;
            int NumRaysPerPixel;
            uint Frame; // current frame/sample index
            float3 SkyColorZenith;
            float3 SkyColorHorizon;
            float3 GroundColor;
            float3 SunLightDirection;
            float SunIntensity;
            float SunFocus;

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

            // Find the first point that the given ray collides with, and return hit info
            HitInfo CalculateRayCollision(Ray ray) {
                HitInfo closestHit = (HitInfo)0;
                // We haven't hit anything yet, so 'closest' hit is infitely far away
                closestHit.dst = 1.#INF;

                // Raycast against all spheres and keep info about the closest hit
                for (int i = 0; i < NumSpheres; i++) {
                    Sphere sphere = Spheres[i];
                    HitInfo hitInfo = RaySphere(ray, sphere.position, sphere.radius);
                    if (hitInfo.didHit && hitInfo.dst < closestHit.dst) {
                        closestHit = hitInfo;
                        closestHit.material = sphere.material;
                    }
                }
                return closestHit;
            }

            // PCG (permuted congruential generator). Thanks to:
            // www.pcg-random.org and www.shadertoy.com/view/XlGcRh
            float RandomValue(inout uint state)
            {
                state = state * 747796405u + 2891336453u;
                uint result = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
                result = (result >> 22u) ^ result;
                return result * (1.0 / 4294967296.0);
            }

            // Random value in normal distribution (with mean=0 and sd=1)
            float RandomValueNormalDistribution(inout uint state)
            {
                // Thanks to https://stackoverflow.com/a/6178290
                float theta = 2 * 3.14159265 * RandomValue(state);
                float rho = sqrt(-2.0 * log(RandomValue(state)));
                return rho * cos(theta);
            }

            // Calculate random direction.
            // Note: there are many alternative methods for computing this,
            // with varying trade-offs between speed and accuracy.
            float3 RandomDirection(inout uint state) {
                // Thanks to https://math.stackexchange.com/a/1585996
                float x = RandomValueNormalDistribution(state);
                float y = RandomValueNormalDistribution(state);
                float z = RandomValueNormalDistribution(state);
                return normalize(float3(x, y, z));
            }

            // Random direction in the hemisphere oriented around the given normal vector
            float3 RandomHemisphereDirection(float3 normal, inout uint rngState) {
                float3 dir = RandomDirection(rngState);
                return dir * sign(dot(normal, dir));
            }

            // Simple background environment lighting
            float3 GetEnvironmentLight(Ray ray)
            {
                float skyGradientT = pow(smoothstep(0, 0.4, ray.dir.y), 0.35);
                float3 skyGradient = lerp(SkyColorHorizon, SkyColorZenith, skyGradientT);
                float sun = pow(max(0, dot(ray.dir, -SunLightDirection)), SunFocus) * SunIntensity;

                // Combine ground, sky, and sun
                float groundToSkyT = smoothstep(-0.01, 0, ray.dir.y);
                float sunMask = groundToSkyT >= 1;
                return lerp(GroundColor, skyGradient, groundToSkyT) + sun * sunMask;
            }

            // Trace the path of a ray of light (in reverse) as it travels from the camera,
            // reflects off objects in the scene, and ends up (hopefully) at a light source.
            float3 Trace(Ray ray, inout uint rngState) {
                float3 incomingLight = 0;
                float3 rayColor = 1;
                
                for (int i = 0; i <= MaxBounceCount; i++){
                    HitInfo hitInfo = CalculateRayCollision(ray);
                    if (hitInfo.didHit) {
                        ray.origin = hitInfo.hitPoint;
                        ray.dir = RandomHemisphereDirection(hitInfo.normal, rngState);
                        
                        RayTracingMaterial material = hitInfo.material;
                        float3 emittedLight = material.emissionColor * material.emissionStrength;
                        float lightStrength = dot(hitInfo.normal, ray.dir);
                        incomingLight += emittedLight * rayColor;
                        rayColor *= material.color.rgb * lightStrength * 2;
                    }
                    else {
                        incomingLight += GetEnvironmentLight(ray) * rayColor;
                        break;
                    }
                }

                return incomingLight;
            }

            // Run for every pixel in the display
            fixed4 frag(v2f i) : SV_Target
            {
                // Create a seed for random number generator
                uint2 numPixels = _ScreenParams.xy;
                uint2 pixelCoord = i.uv * numPixels;
                uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
                uint rngState = pixelIndex + Frame * 719393u;

                // Create ray
                float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
                float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));

                Ray ray;
                ray.origin = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.origin);

                // Calculate pixel color
                float3 totalIncomingLight = 0;
                for (int rayIndex = 0; rayIndex < NumRaysPerPixel; rayIndex++) {
                    totalIncomingLight += Trace(ray, rngState);
                }
                float3 pixelCol = totalIncomingLight / NumRaysPerPixel;
                return float4(pixelCol, 1);
            }
            ENDCG
        }
    }
}
