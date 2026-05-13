using System;
using System.Runtime.InteropServices;

namespace GodotWinUI3Sample.Interop;

[ComImport, Guid("63aad0b8-7c24-40ff-85a8-640d944cc325"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISwapChainPanelNative
{
    [PreserveSig] int SetSwapChain(IntPtr swapChain);
}
