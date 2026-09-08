Shader "JuegoCriminal/Flat Day Night Sky"
{
    Properties
    {
        _SkyColor ("Sky", Color) = (0.3, 0.62, 0.86, 1)
        _SunDirection ("Sun direction", Vector) = (0, 1, 0, 0)
        _MoonDirection ("Moon direction", Vector) = (0, -1, 0, 0)
        _SunVisibility ("Sun visibility", Float) = 1
        _MoonVisibility ("Moon visibility", Float) = 0
        _CloudColor ("Cloud color", Color) = (0.92, 0.95, 1, 1)
        _CloudCoverage ("Cloud coverage", Range(0,1)) = 0.45
        _CloudOpacity ("Cloud opacity", Range(0,1)) = 0.85
        _CloudScale ("Cloud scale", Range(0.5,10)) = 3
        _CloudAngle ("Cloud rotation (radians)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _SkyColor, _SunDirection, _MoonDirection;
                float _SunVisibility, _MoonVisibility;
                float4 _CloudColor;
                float _CloudCoverage, _CloudOpacity, _CloudScale, _CloudAngle;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }
            float Hash(float2 p)
            {
                float3 h = frac(float3(p.xyx) * 0.1031);
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), f.x), f.y);
            }
            float Clouds(float3 dir)
            {
                // Project the upper dome without a longitude seam or a polar pinch.
                float2 p = dir.xz / (max(dir.y, 0.0) + 0.5);
                float s, c;
                sincos(_CloudAngle, s, c);
                p = float2(c*p.x - s*p.y, s*p.x + c*p.y) * _CloudScale;
                float n = Noise(p) * 0.57;
                n += Noise(p * 2.03 + 17.1) * 0.28;
                n += Noise(p * 4.11 + 39.7) * 0.15;
                float threshold = lerp(1.0, 0.12, _CloudCoverage);
                float mask = smoothstep(threshold, threshold + 0.18, n);
                return mask * smoothstep(0.015, 0.18, dir.y) * _CloudOpacity;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);
                float sun = smoothstep(0.9995, 0.99965, dot(dir, normalize(_SunDirection.xyz))) * _SunVisibility;
                float moon = smoothstep(0.9993, 0.9995, dot(dir, normalize(_MoonDirection.xyz))) * _MoonVisibility;
                half3 color = lerp(_SkyColor.rgb, half3(1.4, 1.15, 0.65), sun);
                color = lerp(color, half3(0.72, 0.81, 1.0), moon);
                // Clouds partially obscure the sun/moon rather than drawing behind them.
                float cloud = Clouds(dir);
                color = lerp(color, _CloudColor.rgb * lerp(0.78, 1.0, cloud), cloud);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
