Shader "Universal Render Pipeline/Cat Metro Diorama Lit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _VertexColorWeight("Vertex Color Weight", Range(0, 1)) = 0
        _VertexAlphaWeight("Vertex Alpha Weight", Range(0, 1)) = 0
        _RampThresholds("Three-Step Ramp Thresholds", Vector) = (0.34, 0.68, 0, 0)
        _RimStrength("Rim Strength", Range(0, 0.35)) = 0.14
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DioramaForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _VertexColorWeight;
                half _VertexAlphaWeight;
                half4 _RampThresholds;
                half _RimStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half hasMainLight = step(0.001h,
                    mainLight.color.r + mainLight.color.g + mainLight.color.b);
                half3 warmFallback = half3(1.0h, 0.77h, 0.58h);
                half3 keyColor = lerp(warmFallback, mainLight.color, hasMainLight);
                half key = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half toonRamp = 0.48h
                    + 0.28h * step(_RampThresholds.x, key)
                    + 0.24h * step(_RampThresholds.y, key);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirection)), 3.0h)
                    * _RimStrength;
                half3 lightMix = half3(0.72h, 0.76h, 0.84h)
                    + keyColor * (0.18h + 0.26h * toonRamp)
                    + half3(1.0h, 0.94h, 0.82h) * rim;
                half3 vertexColor = lerp(half3(1.0h, 1.0h, 1.0h),
                    input.color.rgb, _VertexColorWeight);
                half alpha = _BaseColor.a * lerp(1.0h, input.color.a, _VertexAlphaWeight);
                return half4(_BaseColor.rgb * vertexColor * lightMix, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
