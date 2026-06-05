Shader "Custom/URP_HoloCylinder"
{
    Properties
    {
        [HDR] _Color("Base Color", Color) = (0, 1, 0, 0.5) // 基础颜色和透明度
        _EdgeFade("Edge Fade (Hide Caps)", Range(0.001, 0.5)) = 0.1 // 隐藏上下盖子的渐变范围
        _ScanSpeed("Scan Speed", Range(-10, 10)) = 3.0 // 扫描线向下移动的速度
        _ScanDensity("Scan Density", Range(1, 50)) = 20.0 // 扫描线的密集程度
        _ScanIntensity("Scan Intensity", Range(0, 1)) = 0.8 // 扫描线明暗对比度
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        // 【核心魔法】：关闭面剔除。这样玩家能透过前壁看到后壁，形成真正的空心感！
        Cull Off 

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _EdgeFade;
                float _ScanSpeed;
                float _ScanDensity;
                float _ScanIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Unity 默认圆柱体的侧面 UV.y 是从 0 到 1 的。
                // 1. 边缘羽化：用 smoothstep 让靠近顶部 (1.0) 和底部 (0.0) 的区域变透明，完美隐藏盖子。
                float fadeBottom = smoothstep(0.0, _EdgeFade, input.uv.y);
                float fadeTop = smoothstep(1.0, 1.0 - _EdgeFade, input.uv.y);
                float edgeMask = fadeBottom * fadeTop;

                // 2. 动态扫描线：基于时间 _Time.y 产生正弦波动画
                float wave = sin(input.uv.y * _ScanDensity - _Time.y * _ScanSpeed);
                // 将 wave 从 [-1, 1] 映射到我们要的明暗区间
                float scanline = lerp(1.0 - _ScanIntensity, 1.0, wave * 0.5 + 0.5);

                // 3. 最终透明度计算
                float finalAlpha = _Color.a * edgeMask * scanline;

                // 为了防止上下盖子的 UV 映射导致极其微小的瑕疵，做个极致的剔除
                if(finalAlpha < 0.01) discard;

                return half4(_Color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}