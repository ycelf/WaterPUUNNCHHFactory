Shader "WaterFX/Stair Flow"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.16, 0.62, 0.68, 0.78)
        _FoamColor ("Foam Color", Color) = (0.85, 0.98, 1, 0.85)
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.55
        _TextureScale ("Texture Scale", Float) = 2
        _FlowDistance ("Flow Distance", Float) = 0
        _Visibility ("Visibility", Range(0, 1)) = 1
        [Toggle] _ParticleMode ("Particle Mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor;
                float _FoamAmount, _TextureScale, _FlowDistance, _Visibility, _ParticleMode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x), f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_ParticleMode > 0.5)
                {
                    float2 p = input.uv * 2.0 - 1.0;
                    float shape = 1.0 - smoothstep(0.22, 1.0, dot(p, p));
                    float detail = lerp(0.55, 1.0, Noise(input.uv * 7.0));
                    return half4(input.color.rgb, shape * detail * input.color.a * _Visibility);
                }
                // UV.y is distance along the route; subtracting travel moves foam downstream.
                float2 uv = float2(input.uv.x * 6.0, (input.uv.y - _FlowDistance) * _TextureScale);
                float broad = Noise(uv * float2(1.3, 0.8));
                float detail = Noise(uv * float2(3.8, 2.3) + broad);
                float threshold = lerp(0.97, 0.23, _FoamAmount);
                float foam = smoothstep(threshold, threshold + 0.15, broad * 0.65 + detail * 0.35);
                float edge = smoothstep(0.0, 0.055, input.uv.x)
                    * (1.0 - smoothstep(0.945, 1.0, input.uv.x));
                half3 water = _WaterColor.rgb * lerp(0.8, 1.2, broad);
                half3 color = lerp(water, _FoamColor.rgb, foam);
                float alpha = lerp(_WaterColor.a, _FoamColor.a, foam);
                return half4(color, alpha * edge * _Visibility);
            }
            ENDHLSL
        }
    }
}
