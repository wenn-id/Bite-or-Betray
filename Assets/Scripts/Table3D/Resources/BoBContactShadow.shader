// Single-pass URP stereo instanced analytical contact-shadow shader (0 texture memory, 1 combined draw call for the whole table).
Shader "BiteOrBetray/ContactShadow"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+450" "RenderType"="Transparent" }
        Cull Back
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ContactShadow"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // uv in [-1..+1]; color.b encodes corner roundness (1 = circle, 0.25 = rounded rectangle)
                float2 p = input.uv * 2.0 - 1.0;
                half r = max((half)0.15, input.color.b);
                float2 q = abs(p) - (1.0 - r);
                float dist = (length(max(q, 0.0)) + min(max(q.x, q.y), 0.0)) / r;
                half falloff = saturate(1.0 - smoothstep(0.05, 1.0, dist));
                falloff = falloff * falloff;
                return half4(0.035, 0.018, 0.022, input.color.a * falloff);
            }
            ENDHLSL
        }
    }
}
