RWTexture2D<float4> results : register(u0);
Texture1D<float4> colors : register(t0);
Texture1D<float> colorStops : register(t1);
Texture2D<float> values : register(t2);
cbuffer params : register(b0)
{
    float4 contourColor;
    uint contourLevels;
    float maximum;
    float minimum;
}

float toSRgb(float value)
{
    if (!(value > 0.0))
        return 0;
    else if (value <= 0.0031308)
        return (value * 12.92);
    else if (value < 1.0)
        return 1.055f * pow(value, (1.0 / 2.4)) - 0.055f;
    else
        return 1.0;
}

float4 toSRgba(float4 color)
{
    return float4(toSRgb(color.r), toSRgb(color.g), toSRgb(color.b), color.a);
}

float toScRgb(float value)
{
    if (!(value > 0.0))
        return (0.0);
    else if (value <= 0.04045)
        return (value / 12.92);
    else if (value < 1.0)
        return pow((value + 0.055) / 1.055, 2.4);
    else
        return (1.0f);
}

float4 toScRgba(float4 color)
{
    return float4(toScRgb(color.r), toScRgb(color.g), toScRgb(color.b), color.a);
}

float toRatio(int2 id)
{
    return isnan(values[id]) ? 0.0 : clamp((values[id] - minimum) / (maximum - minimum), 0, 1);
}

float getCtLevel(int x, int y, float contour)
{
    float ratio = round(toRatio(int2(x, y)) * contourLevels) / contourLevels;
    return ratio > contour ? 0.7 : (ratio < contour ? 0.3 : 0.5);
}

[numthreads(32, 32, 1)]
void CS(uint3 id : SV_DispatchThreadID)
{
    uint colorsCount;
    colors.GetDimensions(colorsCount);
    float4 result = float4(0, 0, 0, 0);
    float ratio = toRatio(id.xy);
    
    if (colorsCount == 1)
    {
        result = colors[0];
    }
    else if (colorsCount > 1)
    {
        colorsCount--;
        
        for (int index = 0; index < colorsCount; index++)
        {
            if (ratio < colorStops[index])
                break;
        }
        if (index == 0)
            result = colors[0];
        else
        {
            float levelUnit = colorStops[index] - colorStops[index - 1];
            float lerpSegment = (ratio - colorStops[index - 1]) / levelUnit;
            result = lerp(toScRgba(colors[index - 1]), toScRgba(colors[index]), lerpSegment);
        }
    }
        
    if (contourLevels > 0)
    {
        uint width;
        uint height;
        values.GetDimensions(width, height);

        int xPrev = id.x + (id.x <= 0 ? 0 : -1);
        int xNext = id.x + (id.x >= width - 1 ? 0 : 1);
        int yPrev = id.y + (id.y <= 0 ? 0 : -1);
        int yNext = id.y + (id.y >= height - 1 ? 0 : 1);

        float contour = round(ratio * contourLevels) / contourLevels;
            
        float h = -getCtLevel(xPrev, yPrev, contour) + getCtLevel(xNext, yPrev, contour)
        - getCtLevel(xPrev, id.y, contour) * 2 + getCtLevel(xNext, id.y, contour) * 2
        - getCtLevel(xPrev, yNext, contour) + getCtLevel(xNext, yNext, contour);

        float v = -getCtLevel(xPrev, yPrev, contour) - getCtLevel(id.x, yPrev, contour) * 2 - getCtLevel(xNext, yPrev, contour)
        + getCtLevel(xPrev, yNext, contour) + getCtLevel(id.x, yNext, contour) * 2 + getCtLevel(xNext, yNext, contour);

        float edgeRatio = sqrt(h * h + v * v) * contourColor.a;
        if (edgeRatio > 0)
            result = lerp(toScRgba(result), toScRgba(float4(contourColor.rgb, 1)), edgeRatio);
    }

    results[id.xy] = toSRgba(result);
}