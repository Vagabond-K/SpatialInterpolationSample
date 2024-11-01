using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using System.IO;
using System;
using System.Windows;

namespace SpatialInterpolation
{
    public static class ShaderUtilities
    {
        public static ShaderBytecode LoadShaderByteCode(Uri uriResource)
        {
            using (var stream = Application.GetResourceStream(uriResource)?.Stream)
            using (var reader = new StreamReader(stream))
            {
                var sourceCode = reader.ReadToEnd();
                var shaderBytecode = ShaderBytecode.Compile(sourceCode, "CS", "cs_5_0");
                return shaderBytecode;
            }
        }

        public static Texture1D CreateTexture1D(this Device device, int width, SharpDX.DXGI.Format format,
            BindFlags bindFlags = BindFlags.ShaderResource, ResourceUsage usage = ResourceUsage.Default, CpuAccessFlags cpuAccessFlags = CpuAccessFlags.None)
            => new Texture1D(device, new Texture1DDescription
            {
                Width = width,
                ArraySize = 1,
                MipLevels = 0,
                Format = format,
                Usage = usage,
                BindFlags = bindFlags,
                CpuAccessFlags = cpuAccessFlags,
                OptionFlags = ResourceOptionFlags.None,
            });

        public static Texture2D CreateTexture2D(this Device device, int width, int height, SharpDX.DXGI.Format format,
            BindFlags bindFlags = BindFlags.ShaderResource, ResourceUsage usage = ResourceUsage.Default, CpuAccessFlags cpuAccessFlags = CpuAccessFlags.None)
            => new Texture2D(device, new Texture2DDescription
            {
                Width = width,
                Height = height,
                ArraySize = 1,
                MipLevels = 0,
                Format = format,
                BindFlags = bindFlags,
                Usage = usage,
                CpuAccessFlags = cpuAccessFlags,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0)
            });
    }
}
