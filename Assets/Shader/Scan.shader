Shader "Unlit/Scan"
{
    Subshader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }
        Pass
        {
            Name "SeparableGlassBlur"
            ZTest Always
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Blit.hlsl 提供 vertex shader (Vert), input structure (Attributes) and output strucutre (Varyings)
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            #pragma vertex vert //也可以直接用include文件自带的vert
            #pragma fragment frag

            // 中心渐变的范围
            #define centerFadeoutDistance1 1
            #define centerFadeoutDistance2 6

            float3 scanColorHead;
            float3 scanColor;
            float outlineWidth;
            float outlineBrightness;
            float outlineStarDistance;

            float scanLineInterval;
            float scanLineWidth;
            float scanLineBrightness;
            float scanRange;

            float4 scanCenterWS;
            float headScanLineDistance;
            float headScanLineWidth;
            float headScanLineBrightness;

            sampler2D _Pic; //传入普通texture2d纹理

            struct v2f {
                float2 uvs[9] : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(Attributes v) //必须使用include文件自带的Attribute作为输入!!!!
            {
                v2f o;
                float4 pos = GetFullScreenTriangleVertexPosition(v.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(v.vertexID);
       
                o.vertex = pos;

                o.uvs[0] = uv + _ScreenSize.zw * half2(-1, 1) * outlineWidth;
                o.uvs[1] = uv + _ScreenSize.zw * half2(0, 1) * outlineWidth;
                o.uvs[2] = uv + _ScreenSize.zw * half2(1, 1) * outlineWidth;
                o.uvs[3] = uv + _ScreenSize.zw * half2(-1, 0) * outlineWidth;
                o.uvs[4] = uv;
                o.uvs[5] = uv + _ScreenSize.zw * half2(1, 0) * outlineWidth;
                o.uvs[6] = uv + _ScreenSize.zw * half2(-1, -1) * outlineWidth;
                o.uvs[7] = uv + _ScreenSize.zw * half2(0, -1) * outlineWidth;
                o.uvs[8] = uv + _ScreenSize.zw * half2(1, -1) * outlineWidth;

                return o;
            }


            //用uv获取世界坐标
            float3 GetPixelWorldPosition(float2 uv, float depth01)
            {
                //重建世界坐标
                //NDC反透视除法
                float3 farPosCS = float3(uv.x * 2 - 1, uv.y * 2 - 1, 1) * _ProjectionParams.z;
                //反投影
                float3 farPosVS = mul(unity_CameraInvProjection, farPosCS.xyzz).xyz;
                //获得裁切空间坐标
                float3 posVS = farPosVS * depth01;
                //转化为世界坐标   
                float3 posWS = TransformViewToWorld(posVS);
                return posWS;
            }

            half calculaateVerticalOutline(float2 uvs[9])
            {
                // 使用sobel算子计算深度纹理的梯度:-1 0 1 -2 0 2 -1 0 1 
                half color = 0;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[0]).x, _ZBufferParams) * -1;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[1]).x, _ZBufferParams) * -2;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[2]).x, _ZBufferParams) * -1;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[6]).x, _ZBufferParams) * 1;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[7]).x, _ZBufferParams) * 2;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[8]).x, _ZBufferParams) * 1;
                return color;
            }

            half calculateHorizontalOutline(float2 uvs[9])
            {
                // 使用sobel算子计算深度纹理的梯度:-1 0 1 -2 0 2 -1 0 1 
                half color = 0;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[0]).x, _ZBufferParams) * -1;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[3]).x, _ZBufferParams) * -2;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[6]).x, _ZBufferParams) * -1;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[2]).x, _ZBufferParams) * 1;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[5]).x, _ZBufferParams) * 2;
                color += Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, uvs[8]).x, _ZBufferParams) * 1;
                return color;
            }

            half4 frag(v2f i) : SV_Target
            {
                // rebuild world position
                float depth01 = Linear01Depth(SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, i.uvs[4]), _ZBufferParams);
                float3 posWS = GetPixelWorldPosition(i.uvs[4], depth01);

                float distanceToCenter = distance(scanCenterWS, posWS);

                // 头部扫描线
                float scanHeadLine1 = smoothstep(headScanLineDistance + 0.5 * distanceToCenter * 0.03, headScanLineDistance, distanceToCenter);
                float scanHeadLine2 = smoothstep(headScanLineDistance - headScanLineWidth * distanceToCenter * 0.2, headScanLineDistance, distanceToCenter);
                float scanHeadLine = scanHeadLine1 * scanHeadLine2 * scanHeadLine2 * scanHeadLine2 * headScanLineBrightness;
                float4 scanHeadLineColor = float4(scanColorHead*scanHeadLine, scanHeadLine);

                float scanHeadLine3 = smoothstep(headScanLineDistance - headScanLineWidth * distanceToCenter * 0.3 , headScanLineDistance, distanceToCenter);
                float scanHeadLineBlack = scanHeadLine1 * scanHeadLine3 * scanHeadLine3 * scanHeadLine3 * headScanLineBrightness;
                float4 scanHeadLineColorBlack = float4(0, 0, 0, scanHeadLineBlack / 2);

                // 平行扫描线范围遮罩
                float scanLineRange2 = smoothstep(headScanLineDistance - distanceToCenter * 2.5 * scanRange, headScanLineDistance, distanceToCenter);
                float scanLineRange = scanHeadLine1 * scanLineRange2 * scanLineRange2;

                // 中心渐变 
                float centerFadeout = smoothstep(3, 6, distanceToCenter);

                // 平行扫描线
                float wave = frac(distanceToCenter / scanLineInterval);
                float scanLine1 = smoothstep(0.5 - scanLineWidth * distanceToCenter * 0.003, 0.5, wave);
                float scanLine2 = smoothstep(0.5 + scanLineWidth * distanceToCenter * 0.003, 0.5, wave);
                float scanLine = scanLine1 * scanLine2;
                scanLine *= scanLineRange * scanLineBrightness * centerFadeout;
                float4 scanLineColor = float4(scanColor*scanLine, scanLine);


                // 外描边
                half outlineV = calculaateVerticalOutline(i.uvs);
                half outlineH = calculateHorizontalOutline(i.uvs);
                half outline = sqrt(outlineV * outlineV + outlineH * outlineH);
                //近处接近1，中距离接近0，远处为0
                float depthMask = saturate(1 - distanceToCenter * 0.01);
                depthMask *= depthMask;
                half outLineDistanceMask = smoothstep(outlineStarDistance - 10, outlineStarDistance, distanceToCenter);
                outline *= 1000 * depthMask;
                outline = step(1, outline) * outlineBrightness * scanHeadLine1 * outLineDistanceMask;
                float4 outlineColor = float4(scanColor*outline, outline);

                float4 color = scanHeadLineColor + scanHeadLineColorBlack + scanLineColor + outlineColor;
                return color ; //输出世界坐标
            }
            ENDHLSL
        }
    }
}