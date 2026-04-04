using System.Runtime.InteropServices;

namespace ElBruno.LocalEmbeddings.Npu;

/// <summary>
/// Enumerates DXGI adapters to discover NPU hardware for DirectML device selection.
/// </summary>
/// <remarks>
/// On Windows, GPU and NPU devices are exposed as DXGI adapters. This helper
/// inspects adapter descriptions to identify NPU devices (Intel AI Boost, AMD XDNA, etc.)
/// and returns the correct device index for <see cref="NpuOnnxEmbeddingModel"/>.
/// </remarks>
public static class DxgiDeviceHelper
{
    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(in Guid riid, out IntPtr ppFactory);

    private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");

    /// <summary>
    /// Describes a DXGI adapter (GPU, NPU, or software renderer).
    /// </summary>
    /// <param name="Index">The adapter index used as DirectML device ID.</param>
    /// <param name="Description">Human-readable adapter name.</param>
    /// <param name="VendorId">PCI vendor ID (e.g., 0x8086 for Intel).</param>
    /// <param name="DedicatedVideoMemoryBytes">Dedicated video memory in bytes.</param>
    /// <param name="IsLikelyNpu">Whether the description suggests this is an NPU device.</param>
    public sealed record AdapterInfo(
        int Index,
        string Description,
        uint VendorId,
        ulong DedicatedVideoMemoryBytes,
        bool IsLikelyNpu);

    /// <summary>
    /// Enumerates all DXGI adapters on the system.
    /// Returns an empty list on non-Windows platforms or if DXGI is unavailable.
    /// </summary>
    public static List<AdapterInfo> EnumerateAdapters()
    {
        var adapters = new List<AdapterInfo>();

        if (!OperatingSystem.IsWindows())
            return adapters;

        IntPtr factory = IntPtr.Zero;
        try
        {
            int hr = CreateDXGIFactory1(in IID_IDXGIFactory1, out factory);
            if (hr < 0 || factory == IntPtr.Zero)
                return adapters;

            IntPtr vtable = Marshal.ReadIntPtr(factory);
            // IDXGIFactory1::EnumAdapters1 is at vtable slot 12
            IntPtr enumAdapters1Ptr = Marshal.ReadIntPtr(vtable, 12 * IntPtr.Size);
            var enumAdapters1 = Marshal.GetDelegateForFunctionPointer<EnumAdapters1Fn>(enumAdapters1Ptr);

            for (uint i = 0; i < 32; i++)
            {
                IntPtr adapter = IntPtr.Zero;
                try
                {
                    hr = enumAdapters1(factory, i, out adapter);
                    if (hr < 0 || adapter == IntPtr.Zero)
                        break;

                    IntPtr adapterVtable = Marshal.ReadIntPtr(adapter);
                    // IDXGIAdapter1::GetDesc1 is at vtable slot 10
                    IntPtr getDesc1Ptr = Marshal.ReadIntPtr(adapterVtable, 10 * IntPtr.Size);
                    var getDesc1 = Marshal.GetDelegateForFunctionPointer<GetDesc1Fn>(getDesc1Ptr);

                    var desc = new DXGI_ADAPTER_DESC1();
                    hr = getDesc1(adapter, ref desc);
                    if (hr < 0) continue;

                    string description = (desc.Description ?? "").TrimEnd('\0');
                    bool isNpu = IsNpuDescription(description);
                    ulong dedicatedMemory = (ulong)desc.DedicatedVideoMemory;

                    adapters.Add(new AdapterInfo((int)i, description, desc.VendorId, dedicatedMemory, isNpu));
                }
                finally
                {
                    if (adapter != IntPtr.Zero)
                        Marshal.Release(adapter);
                }
            }
        }
        catch (DllNotFoundException)
        {
            // DXGI not available on this platform
        }
        finally
        {
            if (factory != IntPtr.Zero)
                Marshal.Release(factory);
        }

        return adapters;
    }

    /// <summary>
    /// Finds the DXGI device index of the first NPU adapter, or <c>null</c> if none found.
    /// </summary>
    public static int? FindNpuDeviceIndex()
    {
        foreach (var adapter in EnumerateAdapters())
        {
            if (adapter.IsLikelyNpu)
                return adapter.Index;
        }

        return null;
    }

    private static bool IsNpuDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
            return false;

        var upper = description.ToUpperInvariant();
        return upper.Contains("NPU")
            || upper.Contains("AI BOOST")
            || upper.Contains("NEURAL PROCESSOR")
            || upper.Contains("XDNA");
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Fn(IntPtr factory, uint adapter, out IntPtr ppAdapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1Fn(IntPtr adapter, ref DXGI_ADAPTER_DESC1 pDesc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }
}
