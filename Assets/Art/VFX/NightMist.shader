// Night mist puffs: unlit alpha-blended particles with soft-particle depth fade, plus a soft
// screen-centre hole (camera follows the player, so the hole sits on the player) so the player
// stays readable without killing whole puffs.
Shader "Cute Carnage/NightMist"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _SoftFade ("Soft Fade Distance", Float) = 0.8
        _ClearCenter ("Clear Center (viewport)", Vector) = (0.5, 0.4, 0, 0)
        _ClearInner ("Clear Inner Radius (screen height)", Float) = 0.08
        _ClearOuter ("Clear Outer Radius (screen height)", Float) = 0.28
        _ClearMinAlpha ("Clear Hole Leftover Mist", Range(0,1)) = 0.2
        _ClearNoise ("Clear Edge Raggedness", Float) = 0.12
        _NoiseScale ("Edge Noise Scale (world)", Float) = 0.15
        _NoiseSpeed ("Edge Noise Drift Speed", Float) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+100" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _SoftFade;
                float4 _ClearCenter;
                float _ClearInner;
                float _ClearOuter;
                float _ClearMinAlpha;
                float _ClearNoise;
                float _NoiseScale;
                float _NoiseSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.color;
                float2 uv = i.screenPos.xy / i.screenPos.w;

                // Soft particles: fade where the puff meets geometry.
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                c.a *= saturate((sceneZ - i.screenPos.w) / _SoftFade);

                // Soft hole around the player (aspect-corrected, in screen-height units).
                float2 d = uv - _ClearCenter.xy;
                d.x *= _ScreenParams.x / _ScreenParams.y;
                // World-space drifting noise makes the hole edge ragged and alive instead of a clean circle.
                float2 np = i.positionWS.xz * _NoiseScale + _Time.y * _NoiseSpeed * float2(1.0, 0.6);
                float n = ValueNoise(np) * 0.65 + ValueNoise(np * 2.3 + 17.0) * 0.35;
                float r = length(d) + (n - 0.5) * _ClearNoise;
                c.a *= lerp(_ClearMinAlpha, 1.0, smoothstep(_ClearInner, _ClearOuter, r));
                return c;
            }
            ENDHLSL
        }
    }
}
