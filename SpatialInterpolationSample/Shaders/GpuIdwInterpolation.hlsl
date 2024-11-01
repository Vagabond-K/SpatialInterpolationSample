cbuffer params : register(b0)
{
    float searchRadius;
    float weightPower;
}
StructuredBuffer<float3> samples : register(t0);
RWTexture2D<float> results : register(u0);

[numthreads(32, 32, 1)]
void CS(uint3 id : SV_DispatchThreadID)
{
    float sum = 0;
    float weights = 0;
    uint sampleCount;
    uint samplesSize;

    samples.GetDimensions(sampleCount, samplesSize);
    
    for (uint i = 0; i < sampleCount; i++)
    {
        float3 item = samples[i];
        float dist = distance(id.xy, item.xy);
        if (dist <= searchRadius)
        {
            float weight = dist == 0 ? 0.1 : 1.0 / pow(dist, weightPower);
            sum += item.z * weight;
            weights += weight;
        }
    }
    results[id.xy] = sum / weights;
}