StructuredBuffer<float3> samples : register(t0);
RWTexture2D<float> values : register(u0);
cbuffer params : register(b0)
{
    float searchRadius;
    float weightPower;
}

[numthreads(32, 32, 1)]
void CS(uint3 id : SV_DispatchThreadID)
{
    uint sampleCount;
    uint sampleSize;
    samples.GetDimensions(sampleCount, sampleSize);

    float sum = 0;
    float weights = 0;
    for (uint i = 0; i < sampleCount; i++)
    {
        float dist = distance(id.xy, samples[i].xy);
        if (dist <= searchRadius)
        {
            float weight = dist == 0 ? 0.1 : 1.0 / pow(dist, weightPower);
            sum += weight * samples[i].z;
            weights += weight;
        }
    }
    values[id.xy] = sum / weights;
}