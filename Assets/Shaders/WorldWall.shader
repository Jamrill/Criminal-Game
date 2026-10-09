Shader "CriminalGame/World Wall"
{
    Properties
    {
        _BaseMap("Wall texture",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _MetersPerRepeat("World metres per repeat",Float)=4
        _Smoothness("Smoothness",Range(0,1))=.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _MetersPerRepeat;
                half _Smoothness;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS), w=pow(abs(n),8); w/=max(dot(w,1),.0001);
                float3 p=i.positionWS/max(_MetersPerRepeat,.01);
                half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy).rgb*w.x+
                    SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*w.y+
                    SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*w.z;
                InputData d=(InputData)0; d.positionWS=i.positionWS; d.normalWS=n;
                d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord=TransformWorldToShadowCoord(i.positionWS); d.bakedGI=SampleSH(n);
                d.vertexLighting=VertexLighting(i.positionWS,n); d.shadowMask=half4(1,1,1,1);
                d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                SurfaceData s=(SurfaceData)0; s.albedo=color*_BaseColor.rgb; s.alpha=1;
                s.smoothness=_Smoothness; s.occlusion=1; s.normalTS=half3(0,0,1); s.specular=half3(.5,.5,.5);
                half4 result=UniversalFragmentPBR(d,s); result.rgb=MixFog(result.rgb,i.fog); return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
