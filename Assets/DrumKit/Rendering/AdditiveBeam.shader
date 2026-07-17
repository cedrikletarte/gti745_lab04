Shader "DrumKit/AdditiveBeam"
{
    // Unlit, additive, double-sided cone used as a fake volumetric light beam. The per-vertex
    // alpha (baked by LightBeamCone: bright at the apex, fading to nothing at the far end and
    // rim) drives the falloff; _Color is set per-beam via a MaterialPropertyBlock so one shared
    // material serves every colour-cycling spot.
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Strength ("Strength", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Blend One One      // additive
            ZWrite Off
            Cull Off           // both faces add -> volumetric illusion
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Strength;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // vertex alpha = along-beam / rim falloff; additive so the alpha channel itself
                // is irrelevant, the fade is folded into rgb.
                half3 rgb = _Color.rgb * IN.color.a * _Strength;
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
