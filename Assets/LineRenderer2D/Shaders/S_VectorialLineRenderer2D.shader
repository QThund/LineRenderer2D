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

Shader "Game/S_VectorialLineRenderer2D"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        [MaterialToggle] _IsUnlit("Unlit", Float) = 0
        _PointA("Point A", Vector) = (0, 0, 0, 1)
        _PointB("Point B", Vector) = (0, 0, 0, 1)
        _LineColor("Line Color", Color) = (1, 0, 0, 1)
        _BackgroundColor("Background Color", Color) = (0, 0, 0, 0)
        _Thickness("Thickness", Float) = 4.0
        _Origin("Origin", Vector) = (0, 0, 0, 0)
        _ToleranceMultiplier("Tolerance Multiplier", Float) = 0
        _BaseTolerance("Base Tolerance", Float) = 0
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

        BlendOp[_BlendOp]
        Blend 0 [_ColorBlendSrc][_ColorBlendDst],[_AlphaBlendSrc][_AlphaBlendDst]
        Blend 1 [_ColorBlendSrc][_ColorBlendDst],[_AlphaBlendSrc][_AlphaBlendDst]
        Blend 2 Zero One
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
                float4 _LineColor;
                float4 _BackgroundColor;
                float4 _Origin;
                float _Thickness;
                float _ToleranceMultiplier;
                float _BaseTolerance;
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

            float2 TransformToScreenSpace(float4 vInputPoint)
            {
                return ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(vInputPoint.x, vInputPoint.y, 0.0f, 1.0f))).xy * _ScreenParams.xy;
            }

            void CalculateDistanceCorrection(float2 vEndpointA, float2 vEndpointB, float fToleranceMultiplier, float fBaseTolerance, float fThickness, out float outDistanceCorrection)
            {
                #define M_PI 3.1415926535897932384626433832795

                vEndpointA = vEndpointA - fmod(vEndpointA, float2(fThickness, fThickness));
                vEndpointB = vEndpointB - fmod(vEndpointB, float2(fThickness, fThickness));
                vEndpointA = round(vEndpointA);
                vEndpointB = round(vEndpointB);

                // The tolerance is bigger as the slope of the line is closer to any of the 2 axis
                float2 normalizedAbsNextToPrevious = normalize(abs(vEndpointA - vEndpointB));
                float maxValue = max(normalizedAbsNextToPrevious.x, normalizedAbsNextToPrevious.y);
                float minValue = min(normalizedAbsNextToPrevious.x, normalizedAbsNextToPrevious.y);
                float inverseLerp = 1.0f - minValue / maxValue;

                outDistanceCorrection = fBaseTolerance + fToleranceMultiplier * abs(inverseLerp);
            }

            void IsPixelInLine(float2 vEndpointA, float2 vEndpointB, float fThickness, float2 vPointP, float fDistanceCorrection, float2 vOrigin, out bool outIsPixelInLine)
            {
                // The amount of pixels the camera has moved regarding a thickness-wide block of pixels
                vOrigin = fmod(vOrigin, float2(fThickness, fThickness));
                vOrigin = round(vOrigin);

                // This moves the line N pixels, this is necessary due to the camera moves 1 pixel each time and the line may be wider than 1 pixel
                // so this avoids the line jumping from one block (thickness-wide) to the next, and instead its movement is smoother by moving pixel by pixel
                vPointP += float2(fThickness, fThickness) - vOrigin;
                vEndpointA += float2(fThickness, fThickness) - vOrigin;
                vEndpointB += float2(fThickness, fThickness) - vOrigin;

                vEndpointA = vEndpointA - fmod(vEndpointA, float2(fThickness, fThickness));
                vEndpointB = vEndpointB - fmod(vEndpointB, float2(fThickness, fThickness));
                vEndpointA = round(vEndpointA);
                vEndpointB = round(vEndpointB);
                vPointP = vPointP - fmod(vPointP, float2(fThickness, fThickness));
                vPointP = round(vPointP);

                const float OFFSET = 0.055f;

                // There are 2 corner cases: when the line is perfectly horizontal and when it is perfectly vertical
                // It causes a glitch that makes the line fatter
                if (vEndpointA.x == vEndpointB.x)
                {
                    vEndpointA.x -= OFFSET;
                }

                if (vEndpointA.y == vEndpointB.y)
                {
                    vEndpointA.y -= OFFSET;
                }

                float2 ab = vEndpointB - vEndpointA;
                float dotSqrAB = dot(ab, ab);

                float2 ap = vPointP - vEndpointA;
                float dotAP_AB = dot(ap, ab);
                float normProjectionLength = dotAP_AB / dotSqrAB;

                float projectionLength = dotAP_AB / length(ab);
                float2 projectedP = normalize(ab) * projectionLength;

                bool isBetweenAandB = (normProjectionLength >= 0.0f && normProjectionLength <= 1.0f);
                float distanceFromPToTheLine = length(ap - projectedP);

                outIsPixelInLine = isBetweenAandB && distanceFromPToTheLine < fThickness* fDistanceCorrection;
            }

            float4 MainFragmentShader(Varyings i) : SV_TARGET
            {
                float4 mainColor = i.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float4 maskColor = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv);

                clip(mainColor.a == 0.0f ? -1.0f : 1.0f);

                float2 pointA = TransformToScreenSpace(_PointA);
                float2 pointB = TransformToScreenSpace(_PointB);
                float2 pointP = i.screenPos.xy * _ScreenParams.xy;

                float distanceCorrection = 0.0f;
                CalculateDistanceCorrection(_PointA.xy, _PointB.xy, _ToleranceMultiplier, _BaseTolerance, _Thickness, distanceCorrection);

                bool isPixelInLine = false;
                IsPixelInLine(_PointA.xy, _PointB.xy, _Thickness, pointP, distanceCorrection, _Origin.xy, isPixelInLine);

                float4 finalColor = isPixelInLine ? _LineColor : _BackgroundColor;
                bool isEmissive = _IsUnlit;

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
