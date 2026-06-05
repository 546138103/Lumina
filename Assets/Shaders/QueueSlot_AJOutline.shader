Shader "Lumina/Queue Slot AJ Outline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Color", Color) = (0.2, 0.85, 1.0, 1.0)
        [HDR] _FillColor("Transparent Fill Color", Color) = (0.2, 0.85, 1.0, 0.08)
        _OutlineWidth("Outline Width", Range(0.001, 0.08)) = 0.025
        _GlowStrength("Glow Strength", Range(0.2, 4.0)) = 1.4
        _FlickerSpeed("Flicker Speed", Range(0.1, 8.0)) = 2.0
        _FlickerAmount("Flicker Amount", Range(0.0, 1.0)) = 0.45
        _RimPower("Inner Rim Power", Range(0.5, 8.0)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        ZWrite Off

        Pass
        {
            Name "TransparentInnerSilhouette"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half4 _FillColor;
                float _OutlineWidth;
                float _GlowStrength;
                float _FlickerSpeed;
                float _FlickerAmount;
                float _RimPower;
            CBUFFER_END

            float Pulse()
            {
                float wave = sin(_Time.y * _FlickerSpeed) * 0.5 + 0.5;
                return lerp(1.0 - _FlickerAmount, 1.0 + _FlickerAmount, wave);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                float pulse = Pulse();

                half3 color = lerp(_FillColor.rgb, _OutlineColor.rgb, rim) * pulse;
                half alpha = saturate(_FillColor.a * pulse + rim * 0.18 * pulse);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ExpandedGlowingOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            Cull Front
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half4 _FillColor;
                float _OutlineWidth;
                float _GlowStrength;
                float _FlickerSpeed;
                float _FlickerAmount;
                float _RimPower;
            CBUFFER_END

            float Pulse()
            {
                float wave = sin(_Time.y * _FlickerSpeed) * 0.5 + 0.5;
                return lerp(1.0 - _FlickerAmount, 1.0 + _FlickerAmount, wave);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 expandedPositionOS = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(expandedPositionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float pulse = Pulse();
                half3 color = _OutlineColor.rgb * _GlowStrength * pulse;
                half alpha = saturate(_OutlineColor.a * 0.55 * pulse);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
