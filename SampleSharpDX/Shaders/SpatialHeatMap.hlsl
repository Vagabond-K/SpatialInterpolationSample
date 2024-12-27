RWTexture2D<int> heatMap : register(u0);
Texture1D<float4> colors : register(t0);
Texture1D<float> offsets : register(t1);
Texture2D<float> values : register(t2);
cbuffer params : register(b0)
{
    float4 contourColor;
    uint levels;
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

float toOffset(int2 id)
{
    return isnan(values[id]) ? 0.0 : clamp((values[id] - minimum) / (maximum - minimum), 0, 1);
}

float4 toColor(float offset)
{
    uint ranges;
    colors.GetDimensions(ranges);
    ranges--;

    int index;
    for (index = 0; index < ranges; index++)
        if (offset < offsets[index])
            break;

    if (index == 0 || offsets[index] <= offset) return colors[index];

    float range = offsets[index] - offsets[index - 1];
    float alpha = (offset - offsets[index - 1]) / range;
    float4 colorA = colors[index];
    float4 colorB = colors[index - 1];

    return lerp(colorB, colorA, alpha);
}

float posterize(int x, int y)
{
    return floor(toOffset(int2(x, y)) * levels) / levels;
}

float getLineOpacity(int currX, int currY)
{
    if (levels < 2) return 0;

    uint width;
    uint height;
    values.GetDimensions(width, height);

    int prevX = currX + (currX <= 0 ? 0 : -1);
    int prevY = currY + (currY <= 0 ? 0 : -1);
    int nextX = currX + (currX >= width - 1 ? 0 : 1);
    int nextY = currY + (currY >= height - 1 ? 0 : 1);

    float gX = posterize(prevX, prevY) - posterize(nextX, prevY)
        + (posterize(prevX, currY) - posterize(nextX, currY)) * 2
        + posterize(prevX, nextY) - posterize(nextX, nextY);

    float gY = posterize(prevX, prevY) - posterize(prevX, nextY)
        + (posterize(currX, prevY) - posterize(currX, nextY)) * 2
        + posterize(nextX, prevY) - posterize(nextX, nextY);

    gX *= levels / 4.0;
    gY *= levels / 4.0;

    return sqrt(gX * gX + gY * gY);
}

[numthreads(32, 32, 1)]
void CS(uint3 id : SV_DispatchThreadID)
{
    float4 color = toColor(toOffset(id.xy));
    float lineOpacity = getLineOpacity(id.x, id.y) * contourColor.a;
    float4 result = lerp(color, float4(contourColor.rgb, 1), lineOpacity);

    result = toSRgba(result);
    heatMap[id.xy] =
        ((int) (result.a * 255) << 24)
        | ((int) (result.r * 255) << 16)
        | ((int) (result.g * 255) << 8)
        | ((int) (result.b * 255));
}