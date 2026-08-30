Shader "Project/Water/SurfaceFlowFoam"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.38, 0.42, 1)
        _DeepColor ("Deep Color", Color) = (0.005, 0.12, 0.18, 1)
        _FoamColor ("Foam Color", Color) = (0.35, 0.9, 0.82, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _FlowSpeed ("Flow Speed", Float) = 0.65
        _WaveScale ("Wave Scale", Float) = 2.3
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.6
        _FlowDirection ("Flow Direction", Vector) = (1, 0.35, 0, 0)
        _FlowContrast ("Flow Contrast", Float) = 1.35
        _FlowPulseSpeed ("Flow Pulse Speed", Float) = 0.8
        _FlowPulseStrength ("Flow Pulse Strength", Range(0, 1)) = 0.55
        _FoamWidth ("Foam Width", Range(0.001, 1)) = 0.12
        _FoamStrength ("Foam Strength", Range(0, 2)) = 0.95
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.1
        _FoamPulseStrength ("Foam Pulse Strength", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _FlowDirection;
                half _Opacity;
                half _FlowSpeed;
                half _WaveScale;
                half _WaveStrength;
                half _FlowContrast;
                half _FlowPulseSpeed;
                half _FlowPulseStrength;
                half _FoamWidth;
                half _FoamStrength;
                half _EmissionStrength;
                half _FoamPulseStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.worldPos = positionInputs.positionWS;
                output.uv = input.uv;
                return output;
            }

            half Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float timeValue = _Time.y;
                float2 direction = _FlowDirection.xy;
                direction = dot(direction, direction) > 0.001 ? normalize(direction) : float2(1, 0);
                float2 flowUV = input.uv * max(_WaveScale, 0.001);
                float alongFlow = dot(flowUV, direction);
                float acrossFlow = dot(flowUV, float2(-direction.y, direction.x));
                float waveA = sin(alongFlow * 6.28318 + timeValue * _FlowSpeed * 2.2);
                float waveB = sin((alongFlow * 1.7 + acrossFlow * 2.4) * 6.28318 - timeValue * _FlowSpeed * 1.35);
                float flow = saturate(0.5 + 0.5 * (waveA * 0.65 + waveB * 0.35) * _FlowContrast);
                float pulse = 0.5 + 0.5 * sin(timeValue * _FlowPulseSpeed * 6.28318 + alongFlow * 4.0);
                float surfaceVariation = saturate(flow + (pulse - 0.5) * _FlowPulseStrength);
                half3 waterColor = lerp(_DeepColor.rgb, _BaseColor.rgb, 0.42 + surfaceVariation * 0.58);

                float edgeDistance = min(min(input.uv.x, 1.0 - input.uv.x), min(input.uv.y, 1.0 - input.uv.y));
                float edgeFoam = saturate(1.0 - edgeDistance / max(_FoamWidth, 0.001));
                float foamPulse = 1.0 + (pulse - 0.5) * 2.0 * _FoamPulseStrength;
                float foam = saturate(edgeFoam * _FoamStrength * foamPulse);
                half3 finalColor = lerp(waterColor, _FoamColor.rgb, foam);
                finalColor += _FoamColor.rgb * (surfaceVariation * _EmissionStrength * 0.35);
                float alpha = saturate(_Opacity * (0.78 + surfaceVariation * 0.22));
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
