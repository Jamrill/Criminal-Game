Shader "Juego Criminal/Simple Sea"
{
    Properties
    {
        [HDR] _DeepColor("Deep Color", Color) = (0.015, 0.10, 0.18, 1)
        [HDR] _ShallowColor("Shallow Color", Color) = (0.04, 0.48, 0.52, 1)
        _WaveHeight("Wave Height", Range(0, 2)) = 0.35
        _WaveFrequency("Wave Frequency", Range(0.05, 3)) = 0.55
        _WaveSpeed("Wave Speed", Range(0, 5)) = 1.1
        _WaveVariation("Wave Variation", Range(0, 1)) = 1
        _VariationSpeed("Variation Speed", Range(0, 2)) = 0.45
        _RippleStrength("Small Ripple Strength", Range(0, 1)) = 0.22
        _RippleScale("Small Ripple Scale", Range(0.2, 8)) = 2.4
        _RippleSpeed("Small Ripple Speed", Range(0, 5)) = 1.3
        _Smoothness("Smoothness", Range(0, 1)) = 0.72
        _Alpha("Alpha", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SimpleSeaForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                float _WaveHeight;
                float _WaveFrequency;
                float _WaveSpeed;
                float _WaveVariation;
                float _VariationSpeed;
                float _RippleStrength;
                float _RippleScale;
                float _RippleSpeed;
                half _Smoothness;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half waveFactor : TEXCOORD3;
            };

            float GetWaveHeight(float2 position, float time)
            {
                const float2 travelDirection = float2(0.923, 0.385);
                const float2 crestDirection = float2(-0.385, 0.923);
                float forward = dot(position, travelDirection);
                float across = dot(position, crestDirection);
                float variationTime = _Time.y * _VariationSpeed;
                float variationCycle = 0.76 + 0.24 * sin(variationTime * 0.73);
                variationCycle += 0.10 * sin(variationTime * 0.29 + 2.1);
                float dynamicVariation = _WaveVariation * saturate(variationCycle);

                // The phase warp changes the gap between consecutive crests.
                // Lateral warp bends the crest lines without breaking them into bumps.
                float spacingWarp = sin(forward * 0.11 + variationTime * 0.42) * 3.5;
                spacingWarp += sin(forward * 0.037 - variationTime * 0.23) * 1.5;
                float lateralWarp = sin(across * 0.075 + variationTime * 0.43) * 0.6;
                float phase = forward * _WaveFrequency + time;
                phase += (spacingWarp + lateralWarp) * dynamicVariation;

                float wave = sin(phase) * 0.67;
                wave += sin(phase * 0.51 - time * 0.16 + variationTime * 0.12 + 1.7) * 0.24;
                wave += sin(phase * 1.86 + time * 0.21 - variationTime * 0.19) * 0.09;

                // Every wave remains visible; only its height changes from one group to another.
                float groupPattern = sin(forward * 0.083 - variationTime * 0.37);
                groupPattern += sin(forward * 0.031 + variationTime * 0.16 + 1.4) * 0.35;
                groupPattern = saturate(groupPattern * 0.5 + 0.5);
                float grouping = lerp(1.0, lerp(0.58, 1.42, groupPattern), dynamicVariation);
                return wave * grouping * _WaveHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float time = _Time.y * _WaveSpeed;
                float2 wavePosition = positionOS.xz;
                float height = GetWaveHeight(wavePosition, time);
                positionOS.y += height;

                // Finite differences keep lighting consistent with all combined waves.
                const float normalSampleDistance = 0.08;
                float slopeX = (GetWaveHeight(wavePosition + float2(normalSampleDistance, 0), time) - height) / normalSampleDistance;
                float slopeZ = (GetWaveHeight(wavePosition + float2(0, normalSampleDistance), time) - height) / normalSampleDistance;
                float3 normalOS = normalize(float3(-slopeX, 1.0, -slopeZ));

                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                float colorRange = max(_WaveHeight, 0.001);
                output.waveFactor = saturate(height / (colorRange * 2.0) + 0.5);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float2 ripplePosition = input.positionWS.xz * _RippleScale;
                float rippleTime = _Time.y * _RippleSpeed;
                float rippleA = cos(dot(ripplePosition, float2(0.91, 0.41)) + rippleTime);
                float rippleB = cos(dot(ripplePosition, float2(-0.36, 0.93)) * 1.37 - rippleTime * 0.79);
                float rippleC = cos(dot(ripplePosition, float2(0.67, -0.74)) * 0.63 + rippleTime * 0.46);
                float2 rippleSlope = float2(
                    rippleA * 0.91 - rippleB * 0.49 + rippleC * 0.42,
                    rippleA * 0.41 + rippleB * 1.27 - rippleC * 0.47);
                rippleSlope *= _RippleStrength * 0.18;
                normalWS = normalize(normalWS + half3(-rippleSlope.x, 0, -rippleSlope.y));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirection)), 4.0h);

                half3 baseColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, input.waveFactor);
                half3 lighting = mainLight.color * (0.35h + diffuse * 0.65h);
                half3 color = baseColor * lighting;
                color += fresnel * lerp(0.08h, 0.3h, _Smoothness);
                color = MixFog(color, input.fogFactor);
                return half4(color, _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
