Shader "Universal Render Pipeline/Cat Metro Diorama Lit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _VertexColorWeight("Vertex Color Weight", Range(0, 1)) = 0
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
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
                half key = saturate(dot(normalWS, mainLight.direction));
                half3 lightMix = half3(0.72h, 0.76h, 0.84h)
                    + keyColor * (0.18h + 0.26h * key);
                half3 vertexColor = lerp(half3(1.0h, 1.0h, 1.0h),
                    input.color.rgb, _VertexColorWeight);
                return half4(_BaseColor.rgb * vertexColor * lightMix, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
