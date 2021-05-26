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

void IsPixelInLine_float(float fThickness, float2 vPointP, Texture2D tPackedPoints, SamplerState ssArraySampler, float fPackedPointsCount, float fPointsCount, out bool outIsPixelInLine)
{
	// Origin in screen space
	float4 projectionSpaceOrigin = mul(UNITY_MATRIX_VP, float4(0.0f, 0.0f, 0.0f, 1.0f));
	float2 vOrigin = ComputeScreenPos(projectionSpaceOrigin).xy * _ScreenParams.xy;

	// The amount of pixels the camera has moved regarding a thickness-wide block of pixels
	vOrigin = fmod(vOrigin, float2(fThickness, fThickness));
	vOrigin = round(vOrigin);

	// This moves the line N pixels, this is necessary due to the camera moves 1 pixel each time and the line may be wider than 1 pixel
	// so this avoids the line jumping from one block (thickness-wide) to the next, and instead its movement is smoother by moving pixel by pixel
	vPointP += float2(fThickness, fThickness) - vOrigin;

	vPointP = vPointP - fmod(vPointP, float2(fThickness, fThickness));
	vPointP = round(vPointP);

	int pointsCount = round(fPointsCount);

	outIsPixelInLine = false;
		
	for(int t = 0; t < pointsCount - 1; ++t)
	{
		float4 packedPoints = tPackedPoints.Sample(ssArraySampler, float2(float(t / 2) / fPackedPointsCount, 0.0f));
		float4 packedPoints2 = tPackedPoints.Sample(ssArraySampler, float2(float(t / 2 + 1) / fPackedPointsCount, 0.0f));
		
		float2 worldSpaceEndpointA = fmod(t, 2) == 0 ? packedPoints.rg : packedPoints.ba;
		float2 worldSpaceEndpointB = fmod(t, 2) == 0 ? packedPoints.ba : packedPoints2.rg;
		float4 projectionSpaceEndpointA = mul(UNITY_MATRIX_VP, float4(worldSpaceEndpointA.x, worldSpaceEndpointA.y, 0.0f, 1.0f));
		float4 projectionSpaceEndpointB = mul(UNITY_MATRIX_VP, float4(worldSpaceEndpointB.x, worldSpaceEndpointB.y, 0.0f, 1.0f));
		
		// Endpoints in screen space
		float2 vEndpointA = ComputeScreenPos(projectionSpaceEndpointA).xy * _ScreenParams.xy;
		float2 vEndpointB = ComputeScreenPos(projectionSpaceEndpointB).xy * _ScreenParams.xy;

		vEndpointA = round(vEndpointA);
		vEndpointB = round(vEndpointB);
	
		vEndpointA += float2(fThickness, fThickness) - vOrigin;
		vEndpointB += float2(fThickness, fThickness) - vOrigin;

		vEndpointA = vEndpointA - fmod(vEndpointA, float2(fThickness, fThickness));
		vEndpointB = vEndpointB - fmod(vEndpointB, float2(fThickness, fThickness));
		vEndpointA = round(vEndpointA);
		vEndpointB = round(vEndpointB);
		 
		int x = vEndpointA.x;
		int y = vEndpointA.y;
		int x2 = vEndpointB.x;
		int y2 = vEndpointB.y;
		int pX = vPointP.x;
		int pY = vPointP.y;
		int w = x2 - x;
		int h = y2 - y;
		int dx1 = 0, dy1 = 0, dx2 = 0, dy2 = 0;

		if (w<0) dx1 = -fThickness ; else if (w>0) dx1 = fThickness;
		if (h<0) dy1 = -fThickness ; else if (h>0) dy1 = fThickness;
		if (w<0) dx2 = -fThickness ; else if (w>0) dx2 = fThickness;

		int longest = abs(w);
		int shortest = abs(h);

		if (longest <= shortest)
		{
			longest = abs(h);
			shortest = abs(w);

			if (h < 0)
				dy2 = -fThickness; 
			else if (h > 0)
				dy2 = fThickness;
			
			dx2 = 0;
		}

		int numerator = longest >> 1;

		for (int i=0; i <= longest; i+=fThickness)
		{
			if(x == pX && y == pY)
			{
				outIsPixelInLine = true;
				break;
			}

			numerator += shortest;

			if (numerator >= longest)
			{
				numerator -= longest;
				x += dx1;
				y += dy1;
			}
			else
			{
				x += dx2;
				y += dy2;
			}
		}
	}
}
