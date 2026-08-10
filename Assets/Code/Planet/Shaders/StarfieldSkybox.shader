//Procedural starfield skybox — no textures. Stars are hashed from view-direction cells
//in three density/brightness layers, with subtle color temperature variation and an
//optional faint galactic band. Deterministic per _Seed
Shader "StellarForge/Starfield Skybox"
{
    Properties
    {
        _StarDensity ("Star Density", Range(0.5, 4.0)) = 1.5
        _StarBrightness ("Star Brightness", Range(0.0, 3.0)) = 1.2
        _BandColor ("Galactic Band Color", Color) = (0.35, 0.35, 0.5, 1.0)
        _BandIntensity ("Galactic Band Intensity", Range(0.0, 1.0)) = 0.25
        _BandDirection ("Galactic Band Normal", Vector) = (0.2, 1.0, 0.3, 0.0)
        _Seed ("Seed", Float) = 7.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _StarDensity;
            float _StarBrightness;
            fixed4 _BandColor;
            float _BandIntensity;
            float4 _BandDirection;
            float _Seed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + _Seed * 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            //One star layer: quantize the direction into cells, hash a star position and
            //brightness per cell, light pixels near the star point
            float StarLayer(float3 dir, float cells, float threshold)
            {
                float3 cell = floor(dir * cells);
                float h = Hash(cell);

                if (h < threshold)
                    return 0.0;

                //Star position inside the cell (reuse hashes for offsets)
                float3 starPos = (cell + float3(Hash(cell + 1.7), Hash(cell + 4.3), Hash(cell + 9.1))) / cells;
                float d = length(dir - normalize(starPos));

                float size = 0.0015 + Hash(cell + 2.9) * 0.0025;
                float star = smoothstep(size, size * 0.3, d);

                //Brightness variation per star
                return star * (0.3 + 0.7 * Hash(cell + 6.7));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);

                float stars = 0.0;
                stars += StarLayer(dir, 24.0 * _StarDensity, 0.92) * 1.0;    //few bright
                stars += StarLayer(dir, 48.0 * _StarDensity, 0.85) * 0.55;   //medium
                stars += StarLayer(dir, 96.0 * _StarDensity, 0.75) * 0.25;   //many faint

                //Subtle color temperature: warm ↔ cool per direction hash
                float temperature = Hash(floor(dir * 96.0 * _StarDensity));
                fixed3 starColor = lerp(fixed3(1.0, 0.92, 0.82), fixed3(0.85, 0.92, 1.0), temperature);

                //Faint galactic band: brighter near the plane perpendicular to _BandDirection
                float band = pow(1.0 - abs(dot(dir, normalize(_BandDirection.xyz))), 8.0);

                //Distant stars and the galactic band only — the system's own stars are
                //real bodies in the world (SFStar), not painted on the sky
                fixed3 color = starColor * stars * _StarBrightness
                             + _BandColor.rgb * band * _BandIntensity;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
