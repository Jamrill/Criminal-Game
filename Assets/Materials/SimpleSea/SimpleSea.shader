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
        [Normal] _NormalMap("Surface Normal", 2D) = "bump" {}
        _NormalTiling("Normal Tiling", Range(0.02, 2)) = 0.28
        _NormalStrength("Normal Strength", Range(0, 2)) = 0.75
        _NormalSpeed("Normal Speed", Range(0, 1)) = 0.08
        [HDR] _FoamColor("Foam Color", Color) = (0.9, 0.97, 1, 1)
        _FoamAmount("Foam Amount", Range(0, 2)) = 0.9
        _FoamThreshold("Foam Crest Threshold", Range(0.45, 0.95)) = 0.68
        _FoamSharpness("Foam Sharpness", Range(1, 20)) = 8
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

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

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
                float4 _NormalMap_ST;
                float _NormalTiling;
                float _NormalStrength;
                float _NormalSpeed;
                half4 _FoamColor;
                float _FoamAmount;
                float _FoamThreshold;
                float _FoamSharpness;
                half _Smoothness;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half waveFactor : TEXCOORD3;
                float2 uv : TEXCOORD4;
                half foamFactor : TEXCOORD5;
            };

            float WavePacket(float2 position, float2 direction, float frequency,
                float speed, float packetScale, float phaseOffset, float time)
            {
                float2 crestDirection = float2(-direction.y, direction.x);
                float forward = dot(position, direction);
                float across = dot(position, crestDirection);
                float packetTime = _Time.y * _VariationSpeed;

                // Long envelopes break each crest into natural, elongated sections.
                float packetPattern = 0.5 + 0.5 * sin(across * packetScale + packetTime * 0.19 + phaseOffset);
                packetPattern += sin(across * packetScale * 0.41 - packetTime * 0.11 + phaseOffset * 1.7) * 0.16;
                float envelope = smoothstep(0.24, 0.68, packetPattern);
                envelope = lerp(1.0, envelope, _WaveVariation);

                float spacing = sin(forward * 0.074 + packetTime * 0.27 + phaseOffset) * 1.7;
                float crestPhase = forward * frequency + time * speed + spacing * _WaveVariation;
                float crest = sin(crestPhase) + sin(crestPhase * 1.93 + phaseOffset) * 0.12;
                return crest * envelope;
            }

            float GetWaveHeight(float2 position, float time)
            {
                float largeWave = WavePacket(position, normalize(float2(0.96, 0.28)),
                    _WaveFrequency, 1.0, 0.062, 0.2, time) * 0.56;
                float mediumWave = WavePacket(position, normalize(float2(0.88, 0.47)),
                    _WaveFrequency * 1.48, 0.73, 0.093, 2.4, time) * 0.29;
                float smallWave = WavePacket(position, normalize(float2(0.99, 0.12)),
                    _WaveFrequency * 2.15, 1.31, 0.137, 4.7, time) * 0.15;
                return (largeWave + mediumWave + smallWave) * _WaveHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float time = _Time.y * _WaveSpeed;
                float3 basePositionWS = TransformObjectToWorld(positionOS);
                float2 wavePosition = basePositionWS.xz;
                float height = GetWaveHeight(wavePosition, time);
                basePositionWS.y += height;
                positionOS = TransformWorldToObject(basePositionWS);

                // Finite differences keep lighting consistent with all combined waves.
                const float normalSampleDistance = 0.08;
                float slopeX = (GetWaveHeight(wavePosition + float2(normalSampleDistance, 0), time) - height) / normalSampleDistance;
                float slopeZ = (GetWaveHeight(wavePosition + float2(0, normalSampleDistance), time) - height) / normalSampleDistance;
                float3 waveNormalWS = normalize(float3(-slopeX, 1.0, -slopeZ));

                VertexPositionInputs positions = GetVertexPositionInputs(positionOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = waveNormalWS;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                float colorRange = max(_WaveHeight, 0.001);
                output.waveFactor = saturate(height / (colorRange * 2.0) + 0.5);
                const float foamSampleDistance = 0.18;
                const float2 primaryTravelDirection = float2(0.96, 0.28);
                float heightAhead = GetWaveHeight(
                    wavePosition + primaryTravelDirection * foamSampleDistance, time);
                float forwardSlope = (heightAhead - height) / foamSampleDistance;
                float crestMask = saturate((output.waveFactor - _FoamThreshold) * _FoamSharpness);
                float leadingFace = smoothstep(-0.015, 0.12, forwardSlope);
                output.foamFactor = crestMask * leadingFace;
                output.uv = input.uv;
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

                float2 normalUV = input.positionWS.xz * _NormalTiling;
                float normalTime = _Time.y * _NormalSpeed;
                half3 normalA = UnpackNormal(SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, normalUV + float2(normalTime, normalTime * 0.37)));
                half3 normalB = UnpackNormal(SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, normalUV * 1.43 + float2(-normalTime * 0.61, normalTime * 0.83)));
                half2 mappedSlope = (normalA.xy + normalB.xy) * 0.5h * _NormalStrength;
                normalWS = normalize(normalWS + half3(mappedSlope.x, 0, mappedSlope.y));

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirection)), 4.0h);
                half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                half specularPower = lerp(18.0h, 140.0h, _Smoothness);
                half specular = pow(saturate(dot(normalWS, halfDirection)), specularPower);

                half3 baseColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, input.waveFactor);
                half3 lighting = mainLight.color * (0.35h + diffuse * 0.65h);
                half3 color = baseColor * lighting;
                color += fresnel * lerp(0.08h, 0.3h, _Smoothness);
                color += specular * mainLight.color * lerp(0.12h, 0.65h, _Smoothness);
                half foamNoise = SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, normalUV * 0.61 + float2(normalTime * 0.31, -normalTime * 0.24)).r;
                half brokenFoam = smoothstep(0.24h, 0.72h, foamNoise + input.foamFactor * 0.58h);
                half foam = saturate(input.foamFactor * brokenFoam * _FoamAmount);
                half3 foamLighting = _FoamColor.rgb * (0.42h + mainLight.color * (0.25h + diffuse * 0.33h));
                color = lerp(color, foamLighting, foam);
                color = MixFog(color, input.fogFactor);
                return half4(color, _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
