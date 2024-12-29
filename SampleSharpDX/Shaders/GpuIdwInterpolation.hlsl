StructuredBuffer<float3> samples : register(t0);
RWTexture2D<float> values : register(u0);
cbuffer params : register(b0)
{
    float searchRadius;
    float weightPower;
}

[numthreads(32, 32, 1)]
void CS(uint2 id : SV_DispatchThreadID)
{
    uint sampleCount, _;
    samples.GetDimensions(sampleCount, _);

    float sum = 0;
    float weights = 0;
    for (uint i = 0; i < sampleCount; i++)
    {
        float dist = distance(id, samples[i].xy);
        if (dist <= searchRadius)
        {
            float weight = dist == 0 ? 1 : 1 / pow(dist, weightPower);
            sum += weight * samples[i].z;
            weights += weight;
        }
    }
    values[id] = sum / weights;
}