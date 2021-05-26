// Copyright 2020 Alejandro Villalba Avila 
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),  
// to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,  
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: 
// 
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. 
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,  
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER  
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS  
// IN THE SOFTWARE. 

Shader "Game/S_BresenhamLineRenderer2D"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        [MaterialToggle] _IsUnlit("Unlit", Float) = 0
        _PointA("Point A", Vector) = (0, 0, 0, 1)
        _PointB("Point B", Vector) = (0, 0, 0, 1)
        _LineColorA("Line Color A", Color) = (1, 0, 0, 1)
        _LineColorB("Line Color B", Color) = (1, 0, 0, 1)
        _BackgroundColor("Background Color", Color) = (0, 0, 0, 0)
        _Thickness("Thickness", Float) = 4.0
        _DottedLineLength("Dotted Line Length", Float) = 99999.0
        _DottedLineOffset("Dotted Line Offset", Float) = 0
        _LineTexture("Line Texture", 2D) = "white" {}
        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        //[HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        //[HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        //[HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        //[HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        //[HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    } 

    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZTest LEqual
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex MainVertexShader
            #pragma fragment MainFragmentShader
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                float4  color       : COLOR;
                float2	uv          : TEXCOORD0;
                float2	screenPos   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_LineTexture);
            SAMPLER(sampler_LineTexture);

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _IsUnlit;
                float4 _PointA;
                float4 _PointB;
                float4 _LineColorA;
                float4 _LineColorB;
                float4 _BackgroundColor;
                float _Thickness;
                float _DottedLineLength;
                float _DottedLineOffset;
            CBUFFER_END

            Varyings MainVertexShader(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float4 clipVertex = o.positionCS / o.positionCS.w;
                o.screenPos = ComputeScreenPos(clipVertex).xy;
                o.color = v.color;
                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
            #include "Assets/LineRenderer2D/Shaders/S_BresenhamLine.hlsl"
            
            float2 TransformToScreenSpace(float4 vInputPoint)
            {
                return ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(vInputPoint.x, vInputPoint.y, 0.0f, 1.0f))).xy * _ScreenParams.xy;
            }

            float4 MainFragmentShader(Varyings i) : SV_TARGET
            {
                float4 mainColor = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float4 maskColor = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);

                clip(mainColor.a == 0.0f ? -1.0f : 1.0f);

                float2 pointA = TransformToScreenSpace(_PointA);
                float2 pointB = TransformToScreenSpace(_PointB);
                float2 pointP = i.screenPos.xy * _ScreenParams.xy;
                float distanceAB = length(pointA - pointB);
                float distanceAP = length(pointA - pointP);
                float lineProgress = distanceAP / distanceAB;
                float4 pointColor = lerp(_LineColorA, _LineColorB, lineProgress);

                bool isPixelInLine = false;
                IsPixelInLine_float(_PointA.xy, _PointB.xy, _Thickness, pointP, _DottedLineLength, _DottedLineOffset, isPixelInLine);

                float4 finalColor = isPixelInLine ? pointColor : _BackgroundColor;
                finalColor *= SAMPLE_TEXTURE2D(_LineTexture, sampler_LineTexture, i.uv);

                if (_IsUnlit == 0.0f)
                {
                    // Lighting
                    finalColor = CombinedShapeLightShared(finalColor, maskColor, i.screenPos);
                }

                return finalColor;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"

}
