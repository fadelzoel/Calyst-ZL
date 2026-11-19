Shader "Custom/2D_AirBend"
{
    Properties
    {
        _DistortStrength ("Distortion Strength", Range(0, 0.15)) = 0.035
        _TintColor ("Air Sheen Tint", Color) = (0.7, 0.95, 1.0, 0.22)
        _Dir ("Direction", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float _DistortStrength;
            half4 _TintColor;
            float _Dir;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Center UVs (-1 to 1) and carve a crescent wave mask
                float2 p = IN.uv * 2.0 - 1.0;
                p.x *= _Dir;

                float outerDist = length(p);
                float innerDist = length(p + float2(0.42, 0.0));

                float outerMask = smoothstep(1.0, 0.55, outerDist);
                float innerCut = smoothstep(0.35, 0.85, innerDist);
                float crescent = outerMask * innerCut * smoothstep(-0.35, 0.35, p.x);

                // Ripple wave + radial air push
                float ripple = sin((outerDist * 18.0) - (_Time.y * 28.0)) * 0.5 + 0.5;
                float2 offset = normalize(p + float2(0.5, 0.001)) * (_DistortStrength * crescent * (0.6 + 0.4 * ripple));

                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                half3 warpedBg = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + offset).rgb;

                half3 finalCol = lerp(warpedBg, _TintColor.rgb, _TintColor.a * crescent);
                return half4(finalCol, saturate(crescent * 1.4));
            }
            ENDHLSL
        }
    }
}