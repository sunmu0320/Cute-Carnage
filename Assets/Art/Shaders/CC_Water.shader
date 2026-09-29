// Stylised flowing water for URP (needs Depth Texture + Opaque Texture on the URP asset; both are on in PC_RPAsset).
// - Refraction: the scene behind the surface (river bed) is sampled from the opaque texture and tinted by depth.
// - Flow: two procedural noise layers scroll along _FlowDir; their gradient drives normals, refraction wobble and foam.
// - Foam: at the shoreline (depth difference) and as drifting streaks on the surface.
// - _Vertical = 1 maps the pattern on world XY and flows down (waterfall face).
Shader "CuteCarnage/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Tint", Color) = (0.55, 0.9, 0.85, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.3, 0.4, 1)
        _DepthMax ("Depth To Full Color (m)", Float) = 2.2
        _Clarity ("Clarity (0 murky .. 1 clear)", Range(0, 1)) = 0.75
        _SkyColor ("Reflection Color", Color) = (0.7, 0.85, 0.95, 1)
        _FlowDir ("Flow Direction (xy)", Vector) = (0.8, -0.6, 0, 0)
        _FlowSpeed ("Flow Speed", Float) = 0.6
        _WaveScale ("Wave Scale", Float) = 0.35
        _NormalStrength ("Normal Strength", Float) = 0.35
        _Refraction ("Refraction Strength", Float) = 0.03
        _Gloss ("Specular Gloss", Float) = 180
        _SpecStrength ("Specular Strength", Float) = 1.2
        _FoamColor ("Foam Color", Color) = (0.95, 0.98, 1, 1)
        _ShoreFoam ("Shore Foam Width (m)", Float) = 0.6
        _StreakFoam ("Surface Streak Amount", Range(0, 1)) = 0.35
        [Toggle] _Vertical ("Vertical (waterfall)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor, _FlowDir;
                float _DepthMax, _Clarity, _FlowSpeed, _WaveScale, _NormalStrength, _Refraction;
                float _Gloss, _SpecStrength, _ShoreFoam, _StreakFoam, _Vertical;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Gradient noise, roughly -0.5..0.5.
            float2 Hash2 (float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
            }
            float Noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(dot(Hash2(i), f), dot(Hash2(i + float2(1, 0)), f - float2(1, 0)), u.x),
                            lerp(dot(Hash2(i + float2(0, 1)), f - float2(0, 1)), dot(Hash2(i + float2(1, 1)), f - float2(1, 1)), u.x), u.y);
            }
            // Two layers drifting along the flow at different speeds/scales.
            float Waves (float2 uv, float2 dir, float t)
            {
                return Noise(uv - dir * t) + 0.5 * Noise(uv * 2.3 - dir * t * 1.7 + 17.3);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _FlowSpeed;
                float2 dir = _Vertical > 0.5 ? float2(0, -1) : normalize(_FlowDir.xy + 1e-4);
                float2 uv = (_Vertical > 0.5 ? i.positionWS.xy * float2(1, 0.35) : i.positionWS.xz) * _WaveScale;
                if (_Vertical > 0.5) t *= 4.0; // falling water moves faster

                // Normal from the wave height gradient.
                float e = 0.05;
                float h = Waves(uv, dir, t);
                float hx = Waves(uv + float2(e, 0), dir, t) - h;
                float hz = Waves(uv + float2(0, e), dir, t) - h;
                float3 n = normalize(float3(-hx / e * _NormalStrength, 1, -hz / e * _NormalStrength));
                if (_Vertical > 0.5) n = normalize(float3(n.x, n.z, -1)); // face points -z (south)

                // Depth of water behind this pixel.
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float surfaceEye = i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depth = max(0, sceneEye - surfaceEye);

                // Refraction: wobble the lookup, but not onto things in front of the water.
                float2 offset = n.xz * _Refraction * saturate(depth);
                float2 refrUV = screenUV + offset;
                float refrEye = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrEye < surfaceEye) refrUV = screenUV; else depth = refrEye - surfaceEye;
                float3 behind = SampleSceneColor(refrUV);

                // Clear near the bank, deep colour with depth.
                float d01 = saturate(depth / _DepthMax);
                float3 col = lerp(behind * _ShallowColor.rgb, _DeepColor.rgb, saturate(d01 * (1.2 - _Clarity)));

                // Reflection + sun glint.
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light sun = GetMainLight();
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 4);
                col = lerp(col, _SkyColor.rgb, fresnel * 0.6);
                float spec = pow(saturate(dot(n, normalize(sun.direction + viewDir))), _Gloss) * _SpecStrength;
                col += spec * sun.color;

                // Foam: shoreline band broken up by the waves, plus drifting streaks.
                float shore = 1.0 - saturate(depth / max(_ShoreFoam, 1e-3));
                float shoreFoam = step(0.35, shore + h * 0.8) * shore;
                float streak = smoothstep(0.28, 0.4, Noise(uv * float2(0.6, 2.2) - dir * t * 1.3 + 5.1)) * _StreakFoam;
                float foam = saturate(max(shoreFoam, streak * (0.4 + d01)));
                if (_Vertical > 0.5) foam = saturate(0.35 + h * 1.5); // waterfall: mostly white water
                col = lerp(col, _FoamColor.rgb, foam);

                col = MixFog(col, i.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
