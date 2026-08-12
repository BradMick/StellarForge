//Camera-facing glare billboard for a star. A sphere shell can never produce clean glare —
//its brightness varies with viewing angle and shrink-wraps the disc. A billboard with a
//radial falloff is symmetric from every direction, which is what light bloom actually
//looks like. Several of these stack at different sizes and falloffs to build the layered
//depth of a real star's halo (the Space Graphics Toolkit approach)
Shader "StellarForge/Star Glare"
{
    Properties
    {
        _GlareColor ("Glare Color", Color) = (1.0, 0.95, 0.85, 1.0)
        _Intensity ("Intensity", Range(0.0, 8.0)) = 1.0
        //How fast the glow falls off from the centre. Low = wide soft bloom,
        //high = tight bright core
        _Falloff ("Falloff Power", Range(0.5, 12.0)) = 3.0
        //Fraction of the quad the solid core occupies before falloff begins
        _CoreSize ("Core Size", Range(0.0, 0.6)) = 0.12
        //Anamorphic spikes — the four-point flare of a very bright source
        _SpikeStrength ("Spike Strength", Range(0.0, 1.0)) = 0.0
        _SpikeSharpness ("Spike Sharpness", Range(1.0, 64.0)) = 16.0
        _Pulse ("Pulse Amount", Range(0.0, 0.3)) = 0.03
        _PulseSpeed ("Pulse Speed", Float) = 0.5
        //Radial coronal streamers — the filaments that make a corona look structured
        //rather than a smooth fog
        _StreamerStrength ("Streamer Strength", Range(0.0, 2.0)) = 0.0
        _StreamerCount ("Streamer Count", Float) = 26.0
        _StreamerSpeed ("Streamer Speed", Float) = 0.08
    }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _GlareColor;
            float _Intensity;
            float _Falloff;
            float _CoreSize;
            float _SpikeStrength;
            float _SpikeSharpness;
            float _Pulse;
            float _PulseSpeed;
            float _StreamerStrength;
            float _StreamerCount;
            float _StreamerSpeed;

            float Hash1(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            //Billboard: build the quad in view space so it always faces the camera,
            //regardless of how the object is oriented or where the viewer stands
            v2f vert (appdata_base v)
            {
                v2f o;

                //Object origin in view space, then offset by the quad corner
                float3 centerView = UnityObjectToViewPos(float3(0.0, 0.0, 0.0));

                //Scale comes from the object's transform, so one quad mesh serves every size
                float3 scale = float3(
                    length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21),
                    length(unity_ObjectToWorld._m02_m12_m22));

                float2 corner = v.vertex.xy * scale.xy;

                //Point-facing, not screen-aligned. A screen-aligned quad (corners offset in
                //view XY) only lines up with the star's silhouette when the star is at the
                //centre of the view: off-axis, perspective shifts the sphere's silhouette
                //outward while the quad stays put, so the glow slides off the disc and the
                //bare photosphere pokes out one side as a granulated crescent. Building the
                //quad perpendicular to the camera->star direction keeps quad and silhouette
                //sharing a symmetry axis, so they stay concentric from any angle.
                //The camera sits at the origin in view space, so the direction from the star
                //to the camera is just -centerView
                float3 toCamera = normalize(-centerView);
                float3 upReference = abs(toCamera.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
                float3 quadRight = normalize(cross(upReference, toCamera));
                float3 quadUp = cross(toCamera, quadRight);

                float3 cornerView = centerView + quadRight * corner.x + quadUp * corner.y;

                o.pos = mul(UNITY_MATRIX_P, float4(cornerView, 1.0));
                o.uv = v.vertex.xy * 2.0;   //-1..1 across the quad

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float dist = length(i.uv);

                //Outside the quad's inscribed circle contributes nothing
                if (dist > 1.0)
                    return fixed4(0, 0, 0, 1);

                //Solid core, then a smooth power falloff to the rim
                float t = saturate((dist - _CoreSize) / max(1.0 - _CoreSize, 0.001));
                float glow = pow(1.0 - t, _Falloff);

                //Coronal streamers: filaments radiating outward at irregular angles,
                //drifting slowly. Anchored at the disc edge so they read as structure
                //growing OUT of the star rather than lines drawn across the halo
                if (_StreamerStrength > 0.0)
                {
                    float angle = atan2(i.uv.y, i.uv.x);
                    float band = angle / 6.28318 + 0.5;

                    float index = floor(band * _StreamerCount);
                    float local = frac(band * _StreamerCount);

                    //Each streamer gets its own width, brightness and drift
                    float width = 0.25 + Hash1(index * 3.7) * 0.5;
                    float bright = 0.4 + Hash1(index * 7.1) * 0.6;
                    float drift = sin(_Time.y * _StreamerSpeed * 6.28318 + Hash1(index * 11.3) * 6.28318);

                    float across = abs(local - 0.5) / max(width, 0.001);
                    float filament = saturate(1.0 - across);
                    filament *= filament;

                    //Fade in just outside the core and out toward the rim
                    float radial = saturate((dist - _CoreSize) * 3.0) * pow(1.0 - t, _Falloff * 0.45);

                    glow += filament * radial * bright * (0.75 + drift * 0.25) * _StreamerStrength;
                }

                //Anamorphic spikes: bright along the axes, fading with distance
                if (_SpikeStrength > 0.0)
                {
                    float2 axis = abs(normalize(i.uv + 0.00001));
                    float spike = pow(max(axis.x, axis.y), _SpikeSharpness);
                    glow += spike * pow(1.0 - t, 1.5) * _SpikeStrength;
                }

                //Gentle breathing so the star is never perfectly static
                float pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed * 6.28318);

                return fixed4(_GlareColor.rgb * glow * _Intensity * pulse, 1.0);
            }
            ENDCG
        }
    }
}
