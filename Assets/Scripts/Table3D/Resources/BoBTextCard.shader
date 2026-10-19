// Frame, corners, and button states are drawn in one quad/pass, without extra textures.
Shader "BiteOrBetray/TextCard"
{
    Properties
    {
        _State ("State", Float) = 0
        _Aspect ("Aspect", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            CBUFFER_START(UnityPerMaterial)
                float _State;
                float _Aspect;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = TransformObjectToHClip(input.position.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = (input.uv - .5) * float2(_Aspect, 1);
                float2 q = abs(p) - (float2(_Aspect, 1) * .5 - .13);
                float edge = length(max(q, 0)) + min(max(q.x, q.y), 0) - .13;
                float aa = max(fwidth(edge), .001);
                half3 green = half3(.12, .27, .20);
                half3 cream = half3(1, .95, .81);
                half3 orange = half3(.95, .56, .22);
                half3 body = _State < .5 ? cream : _State < 1.5 ? green : _State < 2.5 ? orange : half3(.72, .74, .65);
                half3 border = _State > 2.5 ? green : orange;
                half3 color = lerp(body, border, smoothstep(-.055 - aa, -.055 + aa, edge));
                // The left accent line stays inside the rounded corners.
                color = lerp(color, border, (1 - smoothstep(.065, .085, input.uv.x * _Aspect)) * .7);
                return half4(color, 1 - smoothstep(-aa, aa, edge));
            }
            ENDHLSL
        }
    }
}
