//Lit surface shader that multiplies vertex colors (the terrain ramp is baked into them)
//with an optional albedo texture. Built-in render pipeline.
Shader "StellarForge/Planet Surface"
{
    Properties
    {
        _MainTex ("Albedo (optional)", 2D) = "white" {}
        _ColorMap ("Baked Color Map", 2D) = "gray" {}
        _UseColorMap ("Use Baked Color Map", Range(0,1)) = 0.0
        _MorphStart ("Morph Start Distance", Float) = 0.0
        _MorphEnd ("Morph End Distance", Float) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _ColorMap;
        half _UseColorMap;
        float _MorphStart;
        float _MorphEnd;
        half _Glossiness;
        half _Metallic;

        //Continuous LOD geomorphing (CDLOD-style): UV2/UV3 carry the tile's shape as its
        //parent rendered it. The blend factor comes from each VERTEX's camera distance —
        //parent-shaped at the tile's spawn/merge distance (_MorphStart), fully detailed by
        //_MorphEnd. Per-vertex distance makes the factor continuous across tile borders
        //(no morph seams), makes merges invisible (tiles return to the parent shape before
        //swapping out), and is inherently correct per-camera for split screen.
        //Tiles without morph data leave _MorphStart at 0 → factor 1. Distances are in
        //object space, which is planet-local space (tiles sit at identity transforms)
        void vert (inout appdata_full v)
        {
            float factor = 1.0;

            if (_MorphStart > 0.0)
            {
                float3 cameraObject = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float dist = distance(cameraObject, v.vertex.xyz);
                factor = saturate((_MorphStart - dist) / max(_MorphStart - _MorphEnd, 0.001));
            }

            v.vertex.xyz = lerp(v.texcoord1.xyz, v.vertex.xyz, factor);
            v.normal = normalize(lerp(v.texcoord2.xyz, v.normal, factor));
        }

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_ColorMap;     //cube-face UVs — the tile's sub-rectangle of its face
            float4 color : COLOR;   //vertex colors: CPU-fallback albedo + vegetation alpha
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            //Per-pixel baked color when available (GPU baker), vertex color as fallback
            fixed3 baked = tex2D(_ColorMap, IN.uv_ColorMap).rgb;
            fixed3 surfaceColor = lerp(IN.color.rgb, baked, _UseColorMap);

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb * surfaceColor;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
