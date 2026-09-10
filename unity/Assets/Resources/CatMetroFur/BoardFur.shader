Shader "CatMetro/Board Fur"
{
    Properties
    {
        [MainTexture] _BaseMap("Untouched source atlas", 2D) = "white" {}
        [MainColor] _BaseColor("Base multiplier", Color) = (1,1,1,1)
        _FurColor("Route fur colour", Color) = (1,1,1,1)
        _FurStrength("Fur strength (zero = natural)", Range(0,1)) = 1
        _FurDebug("Show coat mask", Float) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.18
        _Metallic("Metallic", Range(0,1)) = 0
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Float) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion strength", Range(0,1)) = 1
        _EmissionMap("Emission", 2D) = "white" {}
        _EmissionColor("Emission colour", Color) = (0,0,0,1)
        [HideInInspector] _SpecColor("Specular", Color) = (0.2,0.2,0.2,1)
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Color("Legacy colour", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment FurPassFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            // Reuse this installed URP version's surface setup and complete lighting path.
            // Only our albedo operation is original; no fork of package lighting code.
            #define InitializeStandardLitSurfaceData CatMetroOriginalLitSurfaceData
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData

            // These per-renderer properties intentionally use the ordinary material path.
            // Existing passenger property blocks already exclude these draws from SRP batching.
            half4 _FurColor;
            float _FurStrength;
            float _FurDebug;

            float3 FurLinear(float3 colour)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return SRGBToLinear(colour);
                #else
                    return colour;
                #endif
            }

            float FurMask(float3 linearAtlas)
            {
                float high = max(linearAtlas.r, max(linearAtlas.g, linearAtlas.b));
                float low = min(linearAtlas.r, min(linearAtlas.g, linearAtlas.b));
                float saturation = (high - low) / max(high, 0.00001);
                float warmHue = (linearAtlas.g - linearAtlas.b)
                    / max(linearAtlas.r - linearAtlas.b, 0.00001);
                // Conservative fixed-atlas candidate: cream/pink have lower chroma;
                // navy eyes fail the warm ordering. The real mask must still be opened.
                float warm = step(linearAtlas.b, linearAtlas.g) * step(linearAtlas.g, linearAtlas.r);
                return warm * smoothstep(0.82, 0.93, saturation) * smoothstep(0.12, 0.22, warmHue);
            }

            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
            {
                CatMetroOriginalLitSurfaceData(uv, surface);
                float3 source = FurLinear(surface.albedo);
                float3 route = FurLinear(_FurColor.rgb);
                float sourceValue = max(source.r, max(source.g, source.b));
                float routeValue = max(route.r, max(route.g, route.b));
                // Hue replacement retains the source's linear RGB value and stripe contrast;
                // multiplying orange by blue instead would remove most reflected light.
                float3 recoloured = route * (sourceValue / max(routeValue, 0.00001));
                float3 albedo = lerp(source, recoloured, FurMask(source) * saturate(_FurStrength));
                #if defined(UNITY_COLORSPACE_GAMMA)
                    surface.albedo = LinearToSRGB(albedo);
                #else
                    surface.albedo = albedo;
                #endif
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            void FurPassFragment(Varyings input, out half4 colour : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                    , out uint layers : SV_Target1
                #endif
            )
            {
                LitPassFragment(input, colour
                    #ifdef _WRITE_RENDERING_LAYERS
                        , layers
                    #endif
                );
                if (_FurDebug > 0.5)
                {
                    // Raw grayscale mask uses the same UVs, depth and visible triangles.
                    // It deliberately bypasses lighting/fog so protected regions read black.
                    float mask = FurMask(FurLinear(SampleAlbedoAlpha(input.uv,
                        TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb));
                    colour = half4(mask, mask, mask, 1);
                }
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
