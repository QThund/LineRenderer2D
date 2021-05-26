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

Shader "Game/S_BresenhamMultiLineRenderer2D"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        [MaterialToggle] _IsUnlit("Unlit", Float) = 0
        _LineColor("Line Color", Color) = (1, 0, 0, 1)
        _PackedPoints("PackedPoints", 2D) = "clear" {}
        _BackgroundColor("Background Color", Color) = (0, 0, 0, 0)
        _Thickness("Thickness", Float) = 4.0
        _PackedPointsCount("Packed Points Count", Float) = 0.0
        _PointsCount("Points Count", Float) = 0.0
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
        //ZTest LEqual
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
            SAMPLER(SamplerState_Point_Clamp);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_PackedPoints);
            SamplerState sampler_PackedPoints
            {
                Filter = MinMapMipPoint;
                AddressU = Clamp;
                AddressV = Clamp;
            };

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
                float4 _LineColor;
                float4 _BackgroundColor;
                float _Thickness;
                float _PackedPointsCount;
                float _PointsCount;
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
            #include "Assets/LineRenderer2D/Shaders/S_BresenhamMultiLine.hlsl"

            float4 MainFragmentShader(Varyings i) : SV_TARGET
            {
                float4 mainColor = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float4 maskColor = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);

                clip(mainColor.a == 0.0f ? -1.0f : 1.0f);

                float2 pointP = i.screenPos.xy * _ScreenParams.xy;

                bool isPixelInLine = false;
                IsPixelInLine_float(_Thickness, pointP, _PackedPoints, SamplerState_Point_Clamp, _PackedPointsCount, _PointsCount, isPixelInLine);

                float4 finalColor = isPixelInLine ? _LineColor : _BackgroundColor;

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
