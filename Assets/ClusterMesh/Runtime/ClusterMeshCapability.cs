using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public static class ClusterMeshCapability
    {
        public static bool IsSupported()
        {
            return GetUnsupportedReason() == null;
        }

        public static string GetUnsupportedReason()
        {
            if (!SystemInfo.supportsComputeShaders)
                return "Compute shaders are not supported on this device.";
            if (!SystemInfo.supportsIndirectArgumentsBuffer)
                return "Indirect argument buffers are not supported on this device.";
            var device = SystemInfo.graphicsDeviceType;
            if (device == GraphicsDeviceType.OpenGLES2 || device == GraphicsDeviceType.OpenGLES3)
                return "GLES is not supported by ClusterMesh.";
            if (SystemInfo.maxComputeBufferInputsVertex < 4)
                return "Vertex-stage compute buffers are insufficient for ClusterMesh.";
            return null;
        }
    }
}
