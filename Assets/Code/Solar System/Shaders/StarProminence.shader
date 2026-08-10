//Prominences and chromosphere: the ragged, animated plasma layer that sits just above a
//star's photosphere. Rendered on a slightly larger sphere with additive blending and a
//noise-driven alpha, so arcs and loops rise, writhe and fade around the limb — the layer
//that stops a star reading as a smooth ball
Shader "StellarForge/Star Prominence"
{
    Properties
    {
        _StarColor ("Star Color", Color) = (1.0, 0.6, 0.25, 1.0)
        _Intensity ("Intensity", Range(0.0, 4.0)) = 1.4
        _Scale ("Feature Scale", Float) = 4.0
        _Speed ("Churn Speed", Float) = 0.25
        //Higher = fewer, sharper tongues of plasma; lower = a fuller, softer chromosphere
        _Threshold ("Coverage Threshold", Range(0.0, 0.9)) = 0.55
        _EdgeBoost ("Limb Boost", Range(0.0, 8.0)) = 4.0
    }
    SubShader
    {
        //Over the photosphere, under the corona. Depth testing stays ON so the layer
        //cannot paint a hard crust across the disc — only the part of the shell that
        //extends BEYOND the photosphere survives, which is where prominences actually are
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _StarColor;
            float _Intensity;
            float _Scale;
            float _Speed;
            float _Threshold;
            float _EdgeBoost;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objectPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 normal : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.objectPos = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                         lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                         lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y),
                    f.z);
            }

            //Turbulent fbm — the churning look of magnetised plasma
            float Turbulence(float3 p, float t)
            {
                float sum = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;

                for (int i = 0; i < 4; i++)
                {
                    sum += abs(Noise(p * frequency + t * (0.6 + frequency * 0.2)) - 0.5) * 2.0 * amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.1;
                }

                return sum;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.objectPos);
                float t = _Time.y * _Speed;

                //Domain-warped turbulence: plasma tongues that curl and reshape rather
                //than simply scrolling
                float3 warp = float3(
                    Noise(dir * _Scale * 0.7 + t * 0.5),
                    Noise(dir * _Scale * 0.7 + t * 0.5 + 11.3),
                    Noise(dir * _Scale * 0.7 + t * 0.5 + 27.1)) - 0.5;

                float plasma = Turbulence(dir * _Scale + warp * 1.6, t);

                //Soft, wide falloff — plasma has no hard edge. A narrow smoothstep here
                //is what makes the layer read as a cutout crust instead of gas
                float mask = smoothstep(_Threshold, _Threshold + 0.9, plasma);
                mask *= mask;

                //Prominences are only visible arcing off the limb, silhouetted against
                //space. Across the disc they are lost in the photosphere's glare
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float facing = saturate(dot(viewDir, normalize(i.normal)));
                float limbWeight = pow(saturate(1.0 - facing), _EdgeBoost);

                //Hotter cores inside each tongue
                fixed3 color = lerp(_StarColor.rgb, fixed3(1.0, 0.95, 0.8), saturate(plasma - 0.8));

                return fixed4(color * mask * limbWeight * _Intensity, 1.0);
            }
            ENDCG
        }
    }
}
