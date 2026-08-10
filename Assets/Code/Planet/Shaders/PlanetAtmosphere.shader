//Atmosphere shell for SFPlanet. One inside-out sphere serves both views:
//  from space — an additive scattering rim (limb glow) around the planet
//  from the surface — the same backfaces form the sky dome overhead, with the natural
//  bright-horizon gradient falling out of the identical rim math.
//Sun-aware: lit hemisphere glows, night side fades to stars, and the terminator band
//warms toward the sunset color. Cheap single pass — no precomputed scattering
Shader "StellarForge/Planet Atmosphere"
{
    Properties
    {
        _DayColor ("Day Color", Color) = (0.35, 0.55, 1.0, 1.0)
        _SunsetColor ("Sunset Color", Color) = (1.0, 0.45, 0.2, 1.0)
        _Density ("Density", Range(0.0, 3.0)) = 1.0
        _Falloff ("Rim Falloff", Range(0.5, 8.0)) = 2.5
        _SunsetWidth ("Sunset Band Width", Range(1.0, 8.0)) = 3.0
        _SunDirection ("Sun Direction", Vector) = (0.0, 0.0, 1.0, 0.0)
        _FadeRange ("Camera Proximity Fade", Float) = 500.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Front
        ZWrite Off
        //Premultiplied alpha: RGB adds the glow, alpha occludes what's behind — a day
        //sky must HIDE the stars, which pure additive blending can never do
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _DayColor;
            fixed4 _SunsetColor;
            float _Density;
            float _Falloff;
            float _SunsetWidth;
            float4 _SunDirection;
            float _FadeRange;
            float4 _PlanetCenter;
            float _ShellRadius;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.normal);
                float3 towardSun = normalize(-_SunDirection.xyz);

                //Grazing views pass through more air: limb glow from outside, and the
                //bright-horizon / darker-zenith gradient from inside — same term
                float rim = pow(1.0 - abs(dot(viewDir, normal)), _Falloff);

                //Day/night by how much this part of the shell faces the sun
                float sunHeight = dot(normal, towardSun);
                float day = saturate(sunHeight * 3.0 + 0.35);

                //Terminator band: peaks at 1 exactly where the sun grazes the horizon,
                //smoothstep-shaped so its edges ease in and out
                float band = saturate(1.0 - abs(sunHeight) * _SunsetWidth);
                band = band * band * (3.0 - 2.0 * band);

                //Fade fragments near the camera: crossing the shell then reads as a
                //gradient in both directions — the dome fades in as it recedes overhead,
                //the limb softens on approach — instead of the near hemisphere snapping
                //visible the instant the camera passes the surface
                float proximity = saturate(distance(_WorldSpaceCameraPos, i.worldPos) / max(_FadeRange, 1.0));

                //Inside-the-shell factor: 0 in space, 1 under the sky. Only an inhabited
                //sky occludes stars at the zenith — from space the limb stays a glow ring
                //and the planet disc is never veiled
                float cameraAltitude = distance(_WorldSpaceCameraPos, _PlanetCenter.xyz);
                float inside = saturate((_ShellRadius - cameraAltitude) / max(_FadeRange, 1.0));

                float air = 1.0 - abs(dot(viewDir, normal));

                //Overlap, don't crossfade: blue rides the day factor, orange rides the
                //terminator band, and where both exist they ADD toward a bright warm pale.
                //An RGB lerp between near-complementary hues dips through desaturated
                //gray — which reads as a hard seam between two colored bands
                fixed3 sky = _DayColor.rgb * day + _SunsetColor.rgb * (band * 0.9);
                fixed3 glow = sky * (rim * _Density * proximity);

                //Uniform dome brightness when inside by day — daytime zenith is blue, not black
                glow += _DayColor.rgb * (inside * day * 0.3 * _Density * proximity);

                //Forward-scattering halo around the sun when looking sunward through air:
                //wide warm glow plus a hot core. Sits on the horizon at sunset for free —
                //terrain occlusion handles the sun being below the horizon
                float sunAmount = saturate(dot(-viewDir, towardSun));
                float halo = pow(sunAmount, 32.0) * 0.3 + pow(sunAmount, 400.0) * 2.0;
                glow += lerp(_SunsetColor.rgb, fixed3(1.0, 1.0, 1.0), 0.6) * (halo * _Density * proximity);

                //Star occlusion: daylight coverage from air thickness, plus a zenith floor
                //that only applies inside the shell
                float coverage = saturate((air * 0.8 + inside * 0.6) * day * _Density) * proximity;

                return fixed4(glow, coverage);
            }
            ENDCG
        }
    }
}
