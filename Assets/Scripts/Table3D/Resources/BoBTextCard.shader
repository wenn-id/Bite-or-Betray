// Multi-role parlour UI card/plaque/slate/ticket drawn in a single pass without extra textures.
// Role 0: Parchment Info Card (State 0) & Beveled Brass Button Plaque (State 1..3)
// Role 1: Dark Parlour Chalkboard Slate (#1B2426) with brushed-brass frame
// Role 2: Notched Bakery Ticket Stamp (#FCF3DC) for transient effect popups
Shader "BiteOrBetray/TextCard"
{
    Properties
    {
        _State ("State", Float) = 0
        _Role ("Role", Float) = 0
        _Aspect ("Aspect", Float) = 3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
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
                float _Role;
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
                float2 halfExt = float2(_Aspect, 1.0) * 0.5;
                float2 p = (input.uv - 0.5) * float2(_Aspect, 1.0);

                float radius = _Role > 1.5 ? 0.04 : (_State > 0.5 ? 0.16 : 0.11);
                float2 q = abs(p) - (halfExt - radius);
                float edge = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;

                // Role 2: Classical notched bakery-ticket corner scallops
                if (_Role > 1.5)
                {
                    float cornerDist = length(abs(p) - halfExt);
                    edge = max(edge, 0.095 - cornerDist);
                }

                float aa = max(fwidth(edge), 0.0012);

                // Verified WCAG AA / AAA sRGB palette
                half3 parchment  = half3(0.988, 0.953, 0.863); // #FCF3DC
                half3 deepTeal   = half3(0.075, 0.227, 0.212); // #133A36
                half3 amberHover = half3(0.949, 0.663, 0.231); // #F2A93B
                half3 linenDis   = half3(0.749, 0.761, 0.706); // #BFC2B4
                half3 darkSlate  = half3(0.106, 0.141, 0.149); // #1B2426

                half3 caramelRim = half3(0.620, 0.410, 0.190);
                half3 brassRim   = half3(0.860, 0.650, 0.260);
                half3 goldBright = half3(1.000, 0.880, 0.520);
                half3 sageRim    = half3(0.420, 0.470, 0.440);
                half3 ticketRim  = half3(0.760, 0.340, 0.240);

                half3 body;
                half3 border;
                float borderWidth;

                if (_Role > 0.5 && _Role < 1.5)
                {
                    // Chalkboard slate
                    body = darkSlate;
                    border = brassRim;
                    borderWidth = 0.042;
                }
                else if (_Role > 1.5)
                {
                    // Bakery ticket stamp
                    body = parchment;
                    border = ticketRim;
                    borderWidth = 0.036;
                }
                else
                {
                    body = _State < 0.5 ? parchment : _State < 1.5 ? deepTeal : _State < 2.5 ? amberHover : linenDis;
                    border = _State < 0.5 ? caramelRim : _State < 1.5 ? brassRim : _State < 2.5 ? goldBright : sageRim;
                    borderWidth = _State < 0.5 ? 0.026 : 0.052;
                }

                // Subtle inner pin-line / bevel accent strictly near perimeter (keeps central glyph contrast invariant)
                float pinDist = abs(edge + borderWidth + 0.032);
                float pinMask = (1.0 - smoothstep(0.0, 0.008 + aa, pinDist)) * (_State > 2.5 ? 0.0 : 0.35);
                half3 innerBody = lerp(body, border, pinMask * (step(0.12, -edge) < 0.5 ? 1.0 : 0.0));

                half3 color = lerp(innerBody, border, smoothstep(-borderWidth - aa, -borderWidth + aa, edge));
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                return half4(color, 1.0 - smoothstep(-aa, aa, edge));
            }
            ENDHLSL
        }
    }
}
