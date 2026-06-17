Shader "Aetheria/Water Simple"
{
    Properties
    {
        _MainTex ("Water Texture", 2D) = "blue" {}
        _Color ("Water Tint", Color) = (0.1, 0.3, 0.5, 0.85)
        _SpecColor ("Specular", Color) = (0.5, 0.6, 0.7, 1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _FresnelPower ("Fresnel (edge glow)", Range(0.5, 8)) = 3
        _WaveSpeed ("Wave Speed", Float) = 0.5
        _WaveScale ("Wave Scale", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _FresnelPower;
        half _WaveSpeed;
        half _WaveScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            float3 viewDir;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half fresnel = pow(1 - saturate(dot(IN.viewDir, IN.worldNormal)), _FresnelPower);
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = tex.rgb * _Color.rgb;
            o.Alpha = _Color.a * (0.7 + 0.3 * fresnel);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
