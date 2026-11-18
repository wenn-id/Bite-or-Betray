Shader "BiteOrBetray/ChibiFace"
{
    Properties
    {
        [MainTexture] _BaseMap("Original face", 2D) = "white" {}
        _BlinkMap("Closed eyelid artwork", 2D) = "black" {}
        _MouthMap("Open mouth artwork", 2D) = "black" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _FaceBlink("Blink", Range(0,1)) = 0
        _FaceMouth("Mouth", Range(0,1)) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Blend("Blend", Float) = 0
        [HideInInspector] _SrcBlendAlpha("Source alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination alpha", Float) = 0
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BlinkMap);SAMPLER(sampler_BlinkMap);
            TEXTURE2D(_MouthMap);SAMPLER(sampler_MouthMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _FaceBlink, _FaceMouth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 faceSlope:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; half fog:TEXCOORD3; half3 faceNormalWS:TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS=p.positionCS;output.positionWS=p.positionWS;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.faceNormalWS=TransformObjectToWorldNormal(float3(input.faceSlope.x,-1,input.faceSlope.y));
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);output.fog=ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).rgb;
                half4 blink=SAMPLE_TEXTURE2D(_BlinkMap,sampler_BlinkMap,input.uv);
                half4 mouth=SAMPLE_TEXTURE2D(_MouthMap,sampler_MouthMap,input.uv);
                color=lerp(color,blink.rgb,blink.a*_FaceBlink);
                color=lerp(color,mouth.rgb,mouth.a*_FaceMouth);
                // Painted eyelids must not retain the sculpted open-eye lighting relief.
                half expression=max(blink.a*_FaceBlink,mouth.a*_FaceMouth);
                half3 normal=normalize(lerp(normalize(input.normalWS),normalize(input.faceNormalWS),expression));
                half3 viewDir=GetWorldSpaceNormalizeViewDir(input.positionWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half wrapNdl=saturate(dot(normal,light.direction)*0.68h+0.32h);
                half rim=pow(1.0h-saturate(dot(normal,viewDir)),3.4h)*0.16h;
                half3 illumination=SampleSH(normal)+light.color*(wrapNdl*light.shadowAttenuation*light.distanceAttenuation)+half3(1.0h,0.84h,0.58h)*rim;
                color*=illumination*_BaseColor.rgb;
                return half4(MixFog(color,input.fog),_BaseColor.a);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
