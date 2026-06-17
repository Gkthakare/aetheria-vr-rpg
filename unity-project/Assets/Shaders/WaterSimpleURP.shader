Shader "Aetheria/Water Simple URP"
{
    Properties
    {
        _BaseColor ("Water Tint", Color) = (0.1, 0.35, 0.55, 0.88)
        _BaseMap ("Water Texture", 2D) = "blue" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.95
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _BaseMap_ST;
                half _Smoothness;
                half _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
                o.worldPos = posInputs.positionWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 N = normalize(i.worldNormal);
                half3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                half fresnel = pow(1 - saturate(dot(V, N)), _FresnelPower);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                half alpha = _BaseColor.a * (0.7 + 0.3 * fresnel);
                return half4(albedo, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Simple Lit"
}
