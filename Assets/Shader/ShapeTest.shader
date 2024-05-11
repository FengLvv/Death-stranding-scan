Shader "Custom/Sea"
{
    Properties //着色器的输入   
    {
        _BaseMap ("Texture", 2D) = "white" {}
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    CBUFFER_START(UnityPerMaterial)
        //声明变量  
        float4 _BaseMap_ST;
    CBUFFER_END

    TEXTURE2D(_BaseMap); //贴图采样    
    SAMPLER(sampler_BaseMap);

    struct Attributes //顶点着色器  
    {
        float4 color: COLOR;
        float4 positionOS: POSITION;
        float3 normalOS: TANGENT;
        half4 vertexColor: COLOR;
        float2 uv : TEXCOORD0;
    };

    struct Varyings //片元着色器  
    {
        float4 positionCS: SV_POSITION;
        float2 uv: TEXCOORD0;
        half4 vertexColor: COLOR;
    };

    Varyings vert(Attributes v)
    {
        Varyings o;
        o.positionCS = TransformObjectToHClip(v.positionOS);
        o.uv = v.uv;
        o.vertexColor = v.vertexColor;
        return o;
    }
    half4 frag(Varyings i) : SV_Target /* 注意在HLSL中，fixed4类型变成了half4类型*/
    {
        half circle1 = step(0.3, length(i.uv - 0.5));
        half circle2 = step(length(i.uv - 0.5), 0.5);
        half circle = circle1 * circle2;

        half color = 0;
        if (i.uv.y < i.uv.x + 0.15 && i.uv.y > i.uv.x - 0.15 && i.uv.y > -i.uv.x + 0.15 && i.uv.y < -i.uv.x + 1.85|| i.uv.y < -i.uv.x + 1.15 && i.uv.y > -i.uv.x + 0.85 && i.uv.y < i.uv.x + 0.85 && i.uv.y > i.uv.x - 0.85)
        {
            color = 1;
        } 
        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeLine"="UniversalRenderPipeline" //用于指明使用URP来渲染  
        }


        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}