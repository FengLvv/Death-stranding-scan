Shader "TerrianMarks"
{
    Properties
    {
        _IconSize("Icon Size", Float) = 1
        [HDR] _SafeColor("Safe Color", Color) = (1, 1, 1, 1)
        [HDR] _WarningColor("Warning Color", Color) = (1, 1, 0, 1)
        [HDR] _DangerColor("Danger Color", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            // #include "UnityCG.cginc"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            // GPU Instancing
            #pragma multi_compile_instancing
            // #pragma instancing_options procedural:setup

            
            CBUFFER_START(UnityPerMaterial)
                float _IconSize;
                float4 _SafeColor;
                float4 _WarningColor;
                float4 _DangerColor;
            CBUFFER_END
            float colorAlpha;
            
            struct Attributes {
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                uint instanceID : SV_InstanceID;
            };

            #pragma vertex PassVertex
            #pragma fragment PassFragment

            struct Marks {
                float3 position;
                int type;
            };

            StructuredBuffer<Marks> markBuffer;

            Varyings PassVertex(Attributes input)
            {
                Varyings output;
                float2 uv = input.uv;
                uint instanceID = input.instanceID;

                float3 posCenterWS = markBuffer[instanceID].position;
                       
                float3 dirToCam = GetWorldSpaceNormalizeViewDir(posCenterWS);
                float3 xAxis = normalize(cross(float3(0, 1, 0), dirToCam));
                float3 yAxis = normalize(cross(dirToCam, xAxis));

                float3 posWS = posCenterWS;
                posWS += xAxis * (uv.x * 2 - 1) * 0.05 * _IconSize;
                posWS += yAxis * (uv.y * 2 - 1) * 0.05 * _IconSize;

                output.positionCS = TransformWorldToHClip(posWS);
                output.uv = uv;
                output.positionWS = posWS;

                output.instanceID = instanceID;

                return output;
            }

            half4 DrawPattern(int type, float2 uv)
            {
                if (type == 0)
                {
                    half circle1 = step(0.2, length(uv - 0.5));
                    half circle2 = step(length(uv - 0.5), 0.3);
                    half circle = circle1 * circle2;
                    return circle * _SafeColor;
                }
                else if (type == 1)
                {
                    half circle = step(length(uv - 0.5), 0.1);
                    return circle * _SafeColor;
                }
                else if (type == 2)
                {
                    half circle = step(length(uv - 0.5), 0.1);
                    return circle * _WarningColor;
                }
                else
                {
                    float distance = length(uv - 0.5);
                    float lightMask = saturate((distance - 0.25) * (distance - 0.25) * 50 + 0.2);
                    if (uv.y < uv.x + 0.1 && uv.y > uv.x - 0.1 && uv.y > -uv.x + 0.1 && uv.y < -uv.x + 1.95 || uv.y < -uv.x + 1.1 && uv.y > -uv.x + 0.9 && uv.y < uv.x + 0.9 && uv.y > uv.x - 0.9)
                    {
                        return lightMask * _DangerColor;
                    }
                }
                return 0;
            }
            
            half4 PassFragment(Varyings input, out float depth : SV_DEPTH) : SV_Target
            {
                float3 dirToCam = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half4 color = DrawPattern(markBuffer[input.instanceID].type, input.uv);
                color.a *= colorAlpha;

                // 向相机移动，计算裁切空间位置，透视除法后写入深度
                half4 posNDC4Depth = TransformWorldToHClip(input.positionWS + dirToCam  * _IconSize * 0.1 );
                depth = posNDC4Depth.z / posNDC4Depth.w;
                
                return color;
            }
            ENDHLSL
        }

    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}