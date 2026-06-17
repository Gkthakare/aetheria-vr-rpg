Shader "Aetheria/Skybox/Cubemap (URP)"
{
    Properties
    {
        _Tex("Cubemap (HDR)", Cube) = "" {}
        _Tint("Tint", Color) = (.5, .5, .5, .5)
        [Gamma] _Exposure("Exposure", Float) = 1.0
        _Rotation("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Background"
            "Queue" = "Background"
            "PreviewType" = "Skybox"
        }
        LOD 100
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SkyboxCubemap"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_Tex);
            SAMPLER(sampler_Tex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Exposure;
                float _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 rayDirWS : TEXCOORD0;
            };

            float3 RotateAroundYInDegrees(float3 v, float degrees)
            {
                float alpha = degrees * PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                float2 xz = mul(m, v.xz);
                return float3(xz.x, v.y, xz.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 dir = RotateAroundYInDegrees(input.positionOS.xyz, _Rotation);
                o.rayDirWS = TransformObjectToWorldDir(normalize(dir));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.rayDirWS);
                half4 tex = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, dir);
                half3 c = tex.rgb * _Tint.rgb * _Exposure;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
