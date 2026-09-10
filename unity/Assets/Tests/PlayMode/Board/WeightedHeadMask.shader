Shader "Hidden/CatMetro/Tests/WeightedHeadMask"
{
    Properties
    {
        _Threshold ("Head influence threshold", Float) = 0.25
        _HeadOnly ("Head only", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Threshold;
                float _HeadOnly;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float influence : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.influence = input.color.r;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Black body fragments still write depth, including in the zero-weight control.
                float selected = step(_Threshold, input.influence);
                float value = lerp(1.0, selected, _HeadOnly);
                return half4(value, value, value, 1.0);
            }
            ENDHLSL
        }
    }
}
