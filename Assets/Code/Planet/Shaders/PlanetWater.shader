//Planetary water surface for the WaterShell sphere. Built-in render pipeline.
//Works with generated meshes (no UVs or tangents needed): depth-based coloring, shoreline
//foam and wave motion are all derived from world position and the camera depth texture.
//Requires the camera to render a depth texture — WaterShell enables that automatically.
Shader "StellarForge/Planet Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.25, 0.60, 0.70, 0.35)
        _DeepColor ("Deep Color", Color) = (0.03, 0.15, 0.35, 0.90)
        _DepthFade ("Depth Fade Distance", Float) = 0.5
        _FoamColor ("Foam Color", Color) = (0.95, 0.98, 1.0, 0.9)
        _FoamDistance ("Foam Distance", Float) = 0.08
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4.0
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.6
        _Smoothness ("Smoothness", Range(0, 1)) = 0.92
        _WaveHeight ("Wave Height", Float) = 0.005
        _WaveFrequency ("Wave Frequency", Float) = 30.0
        _WaveSpeed ("Wave Speed", Float) = 1.0
        _DepthBias ("Depth Bias (world units)", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        sampler2D_float _CameraDepthTexture;

        fixed4 _ShallowColor;
        fixed4 _DeepColor;
        fixed4 _FoamColor;
        float _DepthFade;
        float _FoamDistance;
        float _FresnelPower;
        float _FresnelStrength;
        half _Smoothness;
        float _WaveHeight;
        float _WaveFrequency;
        float _WaveSpeed;
        float _DepthBias;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
            float4 screenPos;
            float eyeDepth;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            //Two crossing world-space wave trains displaced along the surface normal —
            //no UVs required, and they tile seamlessly around the sphere
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y * _WaveSpeed;
            float wave = sin(dot(wp, float3(1.0, 0.3, 0.7)) * _WaveFrequency + t)
                       + sin(dot(wp, float3(-0.6, 0.2, 1.0)) * _WaveFrequency * 1.31 - t * 1.13);
            v.vertex.xyz += v.normal * wave * 0.5 * _WaveHeight;

            //Constant world-space bias toward the camera: settles water-vs-seabed depth
            //contests at every range with an absolute margin. (Polygon Offset was tried
            //and rejected — its slope-scaled term explodes on grazing beach polygons
            //and made shorelines flicker at low altitude)
            float3 cameraObject = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
            v.vertex.xyz += normalize(cameraObject - v.vertex.xyz) * _DepthBias;

            //Eye-space depth of the (displaced) water surface, for the depth comparison
            COMPUTE_EYEDEPTH(o.eyeDepth);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            //How much water the eye ray passes through before hitting the seabed
            float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,
                               UNITY_PROJ_COORD(IN.screenPos)));
            float rawDiff = sceneZ - IN.eyeDepth;

            //Fail-safe: an absent or invalid depth texture reads as geometry at (or in
            //front of) the water surface. That must render as calm DEEP water — never as
            //planet-wide foam, which is what a zero difference would otherwise produce
            float depthValid = step(0.0001, rawDiff);
            float depthDiff = max(rawDiff, 0.0);

            //Shallow → deep color by water thickness (deep when depth is unavailable)
            float depthBlend = lerp(1.0, saturate(depthDiff / _DepthFade), depthValid);
            fixed4 water = lerp(_ShallowColor, _DeepColor, depthBlend);

            //Fresnel: grazing angles read more opaque and reflective
            float fresnel = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), _FresnelPower);

            //Foam band where the water surface meets terrain — only with valid depth
            float foam = depthValid * (1.0 - saturate(depthDiff / _FoamDistance));
            foam *= foam;

            fixed3 color = lerp(water.rgb, _FoamColor.rgb, foam);
            float alpha = saturate(lerp(water.a, 1.0, max(foam * _FoamColor.a, fresnel * _FresnelStrength)));

            o.Albedo = color;
            o.Smoothness = _Smoothness * (1.0 - foam * 0.7);
            o.Metallic = 0.0;
            o.Alpha = alpha;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Diffuse"
}
