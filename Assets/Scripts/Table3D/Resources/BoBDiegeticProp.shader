// Lightweight URP single-pass stereo instanced vertex-color shader for combined 3D diegetic tabletop props
// (SugarHeartStrip gems/trays, ParlourChalkboard walnut easel, and BrassMenuLectern).
Shader "BiteOrBetray/DiegeticProp"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Back
        ZWrite On
        ZTest LEqual
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                half3 c = input.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    c = SRGBToLinear(c);
                #endif
                // Alpha encodes glossiness (0 = matte walnut/slate, 1 = glossy sugar gem / polished brass)
                o.color = half4(c, input.color.a);
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float3 L = normalize(float3(0.32, 0.86, -0.39));

                float ndl = saturate(dot(N, L));
                float hemi = saturate(N.y * 0.5 + 0.5);
                half3 ambient = lerp(half3(0.36, 0.30, 0.34), half3(0.68, 0.62, 0.54), hemi);
                half3 lit = input.color.rgb * (ambient + ndl * half3(0.62, 0.58, 0.52));

                // Sugar glaze & brass specular highlight driven by vertex alpha
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), lerp(12.0, 42.0, input.color.a)) * input.color.a * 0.48;
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0) * input.color.a * 0.16;
                lit += half3(1.0, 0.96, 0.88) * (spec + rim);

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
