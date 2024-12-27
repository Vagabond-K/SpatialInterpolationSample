using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace SpatialInterpolation.Interpolations
{
    public class GpuIdwInterpolation : GpuInterpolation
    {
        public float SearchRadius { get; set; } = float.PositiveInfinity;
        public float WeightPower { get; set; } = 1;

        private bool disposedValue;
        private Buffer parametersBuffer;

        protected override ShaderBytecode CreateShaderBytecode()
            => ShaderUtilities.LoadShaderByteCode(new Uri($"pack://application:,,,/{typeof(GpuIdwInterpolation).Assembly.GetName().Name};component/Shaders/{nameof(GpuIdwInterpolation)}.hlsl"));

        protected override void Configure(Device device, IEnumerable<SpatialSample> samples, float[,] values)
        {
            var context = device.ImmediateContext;

            if (parametersBuffer?.IsDisposed != false)
            {
                parametersBuffer = new Buffer(device, new BufferDescription((int)Math.Ceiling(Marshal.SizeOf<IdwInterpolationParameters>() / 16d) * 16, BindFlags.ConstantBuffer, ResourceUsage.Default));
                context.ComputeShader.SetConstantBuffer(0, parametersBuffer);
            }

            var parameters = new IdwInterpolationParameters(SearchRadius, WeightPower);
            context.UpdateSubresource(ref parameters, parametersBuffer);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Utilities.Dispose(ref parametersBuffer);
                    base.Dispose(disposing);
                }
                disposedValue = true;
            }
        }

        readonly struct IdwInterpolationParameters
        {
            public IdwInterpolationParameters(in float searchRadius, in float weightPower)
            {
                SearchRadius = searchRadius;
                WeightPower = weightPower;
            }

            public float SearchRadius { get; }
            public float WeightPower { get; }
        }
    }
}
