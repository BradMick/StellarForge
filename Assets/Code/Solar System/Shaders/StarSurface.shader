//Star photosphere: unlit and emissive (a star makes its own light), with limb darkening,
//slow convective granulation, and a hot core tint. Colour comes from the star's derived
//blackbody temperature, so an M-dwarf renders deep orange and an A-star blue-white
Shader "StellarForge/Star Surface"
{
    Properties
    {
        _StarColor ("Star Color", Color) = (1.0, 0.95, 0.85, 1.0)
        //Keep near 1 without HDR: higher just clips every channel to white and throws
        //the star's colour away
        _Intensity ("Emission Intensity", Range(0.5, 4.0)) = 1.15
        _LimbDarkening ("Limb Darkening", Range(0.0, 1.0)) = 0.35
        _GranuleScale ("Granulation Scale", Float) = 22.0
        _GranuleDepth ("Granulation Depth", Range(0.0, 0.6)) = 0.16
        _GranuleSpeed ("Granulation Speed", Float) = 0.08
        //How much the disc centre burns toward white
        _CoreWhite ("Core Whiteness", Range(0.0, 1.0)) = 0.35
        //Starspot darkness — keep low, spots are rare and subtle at a distance
        _SpotDepth ("Starspot Depth", Range(0.0, 0.6)) = 0.15
        //Gentle drift of the convection pattern. Too much smears the granules into
        //ribbons, which is the marble look rather than plasma
        _FlowStrength ("Flow Strength", Range(0.0, 0.5)) = 0.06
        //Higher = harder separation between hot cells and cool lanes
        _Contrast ("Plasma Contrast", Range(0.5, 4.0)) = 0.9
        //How much cooler the limb runs than the disc centre
        _LimbHeatFalloff ("Limb Heat Falloff", Range(0.0, 1.0)) = 0.35
        //Extra heat at the centre of the disc so it blows out toward white
        _CoreHeat ("Core Heat Boost", Range(0.0, 1.0)) = 0.55
        //Bright incandescent ring where the disc meets space
        _RimBoost ("Rim Boost", Range(0.0, 3.0)) = 1.1
        _RimPower ("Rim Tightness", Range(1.0, 16.0)) = 5.0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _StarColor;
            float _Intensity;
            float _LimbDarkening;
            float _GranuleScale;
            float _GranuleDepth;
            float _GranuleSpeed;
            float _CoreWhite;
            float _SpotDepth;
            float _FlowStrength;
            float _Contrast;
            float _LimbHeatFalloff;
            float _CoreHeat;
            float _RimBoost;
            float _RimPower;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 objectPos : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.objectPos = v.vertex.xyz;
                return o;
            }

            //Cheap 3D value noise for the convection cells
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

            //Worley/cellular noise: distance to the nearest of a set of scattered feature
            //points. This is what makes convection GRANULES — discrete bright cells with
            //dark lanes between them. Ridged fBm produces long continuous veins instead,
            //which reads as marble rather than plasma
            float2 Cellular(float3 p, float t)
            {
                float3 cell = floor(p);
                float3 local = frac(p);

                float nearest = 10.0;
                float second = 10.0;

                for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    float3 neighbour = float3(x, y, z);
                    float3 id = cell + neighbour;

                    //Feature site jitters within its cell, drifting over time so the
                    //granules churn and reform rather than sitting still.
                    //(Cannot be named "point" — that is a reserved HLSL keyword)
                    float3 site = float3(
                        Hash(id),
                        Hash(id + 17.3),
                        Hash(id + 43.7));

                    site = 0.5 + 0.42 * sin(t + 6.28318 * site);

                    float d = length(neighbour + site - local);

                    if (d < nearest)
                    {
                        second = nearest;
                        nearest = d;
                    }
                    else if (d < second)
                        second = d;
                }

                //x: distance to nearest point (cell interiors bright, edges dark)
                //y: gap between nearest and second — near zero exactly on a cell wall
                return float2(nearest, second - nearest);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.normal);

                //Limb darkening — real stars fall off steeply toward the edge of the disc,
                //which is most of what stops a sphere reading as a flat ball
                float facing = saturate(dot(viewDir, normal));
                float limb = lerp(1.0 - _LimbDarkening, 1.0, pow(facing, 0.45));

                //Flowing distortion: warp the sample position by a slowly evolving noise
                //field so the surface CHURNS like fluid instead of twinkling in place.
                //This is what separates a living photosphere from animated static
                float3 basePos = normalize(i.objectPos);
                float flowTime = _Time.y * _GranuleSpeed;

                float3 flow = float3(
                    Noise(basePos * _GranuleScale * 0.35 + flowTime * 0.6),
                    Noise(basePos * _GranuleScale * 0.35 + flowTime * 0.6 + 19.7),
                    Noise(basePos * _GranuleScale * 0.35 + flowTime * 0.6 + 43.1)) - 0.5;

                float3 samplePos = (basePos + flow * _FlowStrength) * _GranuleScale;

                //Granulation: cellular convection. Cell interiors are hot rising plasma,
                //the lanes between them are cooler sinking gas — the actual structure of
                //a photosphere, at two scales
                float2 cellNoise = Cellular(samplePos, flowTime * 2.0);

                //Bright inside cells, dark in the lanes
                float granules = saturate(cellNoise.y * 2.2);
                granules = pow(granules, _Contrast);

                //Finer granules riding on top of the large ones
                float2 fineNoise = Cellular(samplePos * 2.6, flowTime * 3.0);
                granules = granules * 0.7 + saturate(fineNoise.y * 2.0) * 0.3;

                //Supergranulation: large slow cells the granules ride on, giving broad
                //hot and cool regions across the disc
                float3 superPos = (basePos + flow * _FlowStrength * 0.5) * _GranuleScale * 0.22;
                float2 superNoise = Cellular(superPos, _Time.y * _GranuleSpeed * 0.7);
                float cells = saturate(superNoise.y * 1.8);

                //Starspots: rare, small and shallow. Big dark blotches read as mould
                //rather than photosphere, so this stays a subtle darkening at most
                float3 spotPos = basePos * _GranuleScale * 0.85;
                float spots = Noise(spotPos + _Time.y * _GranuleSpeed * 0.15);
                spots = smoothstep(0.80, 0.94, spots) * _SpotDepth;

                //Bright faculae network in the granule lanes
                float faculae = pow(saturate(granules - 0.6) * 2.2, 2.0) * 0.5;

                //Heat field: hottest inside granules in the disc centre, coolest in the
                //lanes out at the limb. Drives the whole colour ramp, which is what gives
                //a star its white-hot core and deep ember rim.
                //The centre gets an additional boost so it genuinely blows out to white
                float coreHeat = pow(facing, 1.6) * _CoreHeat;

                float heat = saturate(granules * (0.5 + cells * 0.5))
                           * lerp(_LimbHeatFalloff, 1.0, pow(facing, 0.7))
                           * (1.0 - spots);

                heat = saturate(heat + coreHeat);

                //Blackbody ramp anchored to the star's own colour. The disc of a bright
                //star is overwhelmingly white-hot — its colour shows in the cooler lanes
                //and at the rim, not across the whole face
                fixed3 emberColor = _StarColor.rgb * fixed3(0.85, 0.42, 0.12);
                fixed3 midColor   = lerp(_StarColor.rgb, fixed3(1.0, 0.92, 0.62), 0.55);
                fixed3 hotColor   = lerp(_StarColor.rgb, fixed3(1.0, 1.0, 0.97), _CoreWhite);

                fixed3 color = heat < 0.5
                    ? lerp(emberColor, midColor, saturate(heat * 2.0))
                    : lerp(midColor, hotColor, saturate((heat - 0.5) * 2.0));

                //Hard bright rim: the last sliver of the disc against space flares up.
                //This edge is a large part of what makes a star read as incandescent
                float rim = pow(saturate(1.0 - facing), _RimPower);
                color += lerp(_StarColor.rgb, fixed3(1.0, 0.85, 0.45), 0.5) * rim * _RimBoost;

                //Gentle overall falloff — the rim above supplies the edge definition
                float brightness = lerp(1.0 - _LimbDarkening, 1.0, pow(facing, 0.45));

                return fixed4(color * brightness * _Intensity + hotColor * faculae * 0.25, 1.0);
            }
            ENDCG
        }
    }
}
