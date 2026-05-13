// godot_winui3_embed.cs
// C# P/Invoke bridge for the Godot WinUI3 embedding layer.
//
// This file is part of:
//   GODOT ENGINE - https://godotengine.org
// Copyright (c) 2014-present Godot Engine contributors (see AUTHORS.md).
// Distributed under the MIT licence — see LICENSE.txt in the engine root.
//
// Usage
// -----
// 1. Build Godot as a shared library (library_type=shared_library).
//    The resulting DLL is named "godot.windows.<target>.<arch>.dll".
//    Update the DLL_NAME constant below if your output name differs.
// 2. Place the DLL next to your WinUI3 application or add it to PATH.
// 3. Call GodotWinUI3Embed.SetSwapChainPanel() after engine init.
// 4. Wire up SizeChanged / pointer / key handlers to the remaining helpers.

using System;
using System.Runtime.InteropServices;

namespace Godot.WinUI3
{
    /// <summary>
    /// Low-level P/Invoke declarations that map directly to the C ABI
    /// exported by <c>godot_winui3_embed.cpp</c>.
    /// Prefer the <see cref="GodotWinUI3Embed"/> wrapper class instead.
    /// </summary>
    internal static class GodotWinUI3Native
    {
        // Update if the Godot shared-library output name differs in your build.
        private const string DLL_NAME = "godot";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void GodotLogDelegate(IntPtr message, int level);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_log_callback(GodotLogDelegate? callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_embedded_parent_hwnd(IntPtr hwnd);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int godot_winui3_engine_setup(int argc, IntPtr argv);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int godot_winui3_engine_start();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int godot_winui3_engine_iteration();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_engine_shutdown();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_swap_chain_panel(
            int windowId,
            IntPtr panelNative);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_notify_panel_resize(
            int windowId,
            int width,
            int height);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_composition_scale(
            int windowId,
            float scaleX,
            float scaleY);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_inject_mouse_button(
            int windowId,
            int button,
            int pressed,
            float x,
            float y);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_inject_mouse_motion(
            int windowId,
            float x,
            float y,
            float relX,
            float relY);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_inject_key(
            int windowId,
            int keycode,
            int pressed,
            int echo,
            uint character);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_inject_mouse_wheel(
            int windowId,
            float x,
            float y,
            float deltaX,
            float deltaY);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_input_mode(int mode);

        // Host <-> Engine messaging.

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void GodotHostMsgDelegate(IntPtr methodUtf8, IntPtr argsJsonUtf8);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_host_message_callback(GodotHostMsgDelegate? callback);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_set_call_return(IntPtr jsonUtf8);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int godot_winui3_call_engine(IntPtr methodUtf8, IntPtr argsJsonUtf8, out IntPtr retJsonUtf8);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void godot_winui3_free_string(IntPtr str);
    }

    /// <summary>Severity level passed to the log callback.</summary>
    public enum GodotLogLevel : int
    {
        Print   = 0,
        Warning = 1,
        Error   = 2,
    }

    /// <summary>
    /// Controls how the embedded Godot window receives input.
    /// </summary>
    public enum GodotWinUI3InputMode : int
    {
        /// <summary>
        /// Default. Win32 WM_* messages reach Godot's WndProc normally.
        /// Mouse events arrive via Win32 hit-testing on the child HWND;
        /// keyboard requires a separate hook or InputKeyboardSource wiring.
        /// </summary>
        Native = 0,

        /// <summary>
        /// Godot's WndProc suppresses all native mouse/keyboard WM_* messages.
        /// The host must inject every input event via the GodotWinUI3Embed.Inject*
        /// methods. Use when the SwapChainPanel handles pointer/key events in XAML.
        /// </summary>
        Xaml = 1,
    }

    /// <summary>
    /// Godot mouse-button indices, matching the <c>MouseButton</c> enum in
    /// <c>core/input/input_enums.h</c>.
    /// </summary>
    public enum GodotMouseButton : int
    {
        None   = 0,
        Left   = 1,
        Right  = 2,
        Middle = 3,
        WheelUp    = 4,
        WheelDown  = 5,
        WheelLeft  = 6,
        WheelRight = 7,
        XButton1   = 8,
        XButton2   = 9,
    }

    /// <summary>
    /// Typed helpers for injecting WinUI3 input/display events into a
    /// shared-library Godot instance via P/Invoke.
    /// </summary>
    /// <remarks>
    /// All methods are thread-safe to call from the WinUI3 UI thread because
    /// the underlying engine functions are designed to be invoked from the host
    /// application thread. However, the engine must have completed its
    /// <c>Main::setup()</c> phase before any of these calls are made.
    /// </remarks>
    public static class GodotWinUI3Embed
    {
        // Pinned delegate — must outlive the native callback registration.
        private static GodotWinUI3Native.GodotLogDelegate? _logDelegatePin;

        /// <summary>
        /// Installs a callback that receives all Godot print / warning / error output.
        /// Call this BEFORE <see cref="EngineSetup"/> to capture setup failures.
        /// Pass <c>null</c> to uninstall.
        /// </summary>
        /// <param name="callback">
        /// Receives the formatted message and its <see cref="GodotLogLevel"/>.
        /// Invoked on whichever thread triggered the log event — do not call any
        /// <c>GodotWinUI3Embed</c> method from inside the callback.
        /// </param>
        public static void SetLogCallback(Action<string, GodotLogLevel>? callback)
        {
            if (callback == null)
            {
                GodotWinUI3Native.godot_winui3_set_log_callback(null);
                _logDelegatePin = null;
                return;
            }

            _logDelegatePin = (msgPtr, level) =>
            {
                string msg = Marshal.PtrToStringUTF8(msgPtr) ?? string.Empty;
                callback(msg, (GodotLogLevel)level);
            };
            GodotWinUI3Native.godot_winui3_set_log_callback(_logDelegatePin);
        }

        /// <summary>
        /// Sets the host HWND that the engine's main window should be re-parented
        /// into (as a <c>WS_CHILD</c>). Must be called BEFORE
        /// <see cref="EngineSetup"/> — DisplayServer reads this during init.
        /// </summary>
        /// <param name="hostHwnd">
        /// HWND of the WinUI3 window. Obtain via
        /// <c>WinRT.Interop.WindowNative.GetWindowHandle(window)</c>.
        /// Pass <see cref="IntPtr.Zero"/> to clear.
        /// </param>
        public static void SetEmbeddedParentHwnd(IntPtr hostHwnd)
        {
            GodotWinUI3Native.godot_winui3_set_embedded_parent_hwnd(hostHwnd);
        }

        /// <summary>
        /// Initialises the embedded Godot engine in this process. Calls
        /// <c>Main::setup()</c> internally. After this returns true, bind the
        /// <c>SwapChainPanel</c> with <see cref="SetSwapChainPanel"/> and call
        /// <see cref="EngineStart"/>.
        /// </summary>
        /// <param name="args">
        /// Engine arguments. <c>args[0]</c> is treated as the program name (use
        /// any placeholder like <c>"godot"</c>); subsequent entries are
        /// forwarded as-is. Typical: <c>{ "godot", "--path", projectDir,
        /// "--rendering-driver", "d3d12" }</c>.
        /// </param>
        /// <returns><c>true</c> on success, <c>false</c> if init failed.</returns>
        public static bool EngineSetup(string[] args)
        {
            if (args == null || args.Length < 1)
                throw new ArgumentException("args must contain at least argv[0].", nameof(args));

            // Marshal string[] -> char**. UTF-8 to match the engine's argv parsing.
            IntPtr[] utf8Ptrs = new IntPtr[args.Length];
            IntPtr argv = Marshal.AllocHGlobal(IntPtr.Size * args.Length);
            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(args[i] + '\0');
                    utf8Ptrs[i] = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, utf8Ptrs[i], bytes.Length);
                    Marshal.WriteIntPtr(argv, i * IntPtr.Size, utf8Ptrs[i]);
                }
                return GodotWinUI3Native.godot_winui3_engine_setup(args.Length, argv) != 0;
            }
            finally
            {
                for (int i = 0; i < utf8Ptrs.Length; i++)
                {
                    if (utf8Ptrs[i] != IntPtr.Zero)
                        Marshal.FreeHGlobal(utf8Ptrs[i]);
                }
                Marshal.FreeHGlobal(argv);
            }
        }

        /// <summary>
        /// Starts the loaded project (calls <c>Main::start()</c> internally).
        /// Call after <see cref="EngineSetup"/> and after the swap chain panel
        /// has been bound.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public static bool EngineStart()
        {
            return GodotWinUI3Native.godot_winui3_engine_start() != 0;
        }

        /// <summary>
        /// Runs a single frame of the engine main loop. Drive this from a
        /// 60Hz <c>DispatcherQueueTimer</c> on the UI thread.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the engine wants to quit (stop the timer and call
        /// <see cref="EngineShutdown"/>). <c>false</c> to keep iterating.
        /// </returns>
        public static bool EngineIteration()
        {
            return GodotWinUI3Native.godot_winui3_engine_iteration() != 0;
        }

        /// <summary>
        /// Shuts down the engine and releases all resources. Idempotent.
        /// </summary>
        public static void EngineShutdown()
        {
            GodotWinUI3Native.godot_winui3_engine_shutdown();
        }

        /// <summary>
        /// Passes the <c>ISwapChainPanelNative</c> pointer for a Godot window
        /// to the engine so it can render into the WinUI3 panel.
        /// </summary>
        /// <remarks>
        /// Call this once after engine initialisation and before the first
        /// rendered frame.  Obtain <paramref name="panelNative"/> in C# by
        /// querying the <c>ISwapChainPanelNative</c> interface from the panel:
        /// <code>
        ///   // Godot AddRefs the pointer internally, so we Release our reference.
        ///   IntPtr ptr = Marshal.GetComInterfaceForObject(
        ///       swapChainPanel, typeof(ISwapChainPanelNative));
        ///   try {
        ///       GodotWinUI3Embed.SetSwapChainPanel(windowId, ptr);
        ///   } finally {
        ///       Marshal.Release(ptr);
        ///   }
        /// </code>
        /// </remarks>
        /// <param name="windowId">
        /// Godot window ID.  Pass <c>0</c> for the main window
        /// (<c>DisplayServer.MAIN_WINDOW_ID</c>).
        /// </param>
        /// <param name="panelNative">
        /// Raw <c>ISwapChainPanelNative*</c> interface pointer, as obtained
        /// from <c>ISwapChainPanelNative</c> COM interop.
        /// </param>
        public static void SetSwapChainPanel(int windowId, IntPtr panelNative)
        {
            GodotWinUI3Native.godot_winui3_set_swap_chain_panel(windowId, panelNative);
        }

        /// <summary>
        /// Notifies the engine that the <c>SwapChainPanel</c> was resized.
        /// </summary>
        /// <remarks>
        /// Wire this up to the <c>SwapChainPanel.SizeChanged</c> event.
        /// Multiply by <c>XamlRoot.RasterizationScale</c> to convert XAML logical units
        /// (DIPs) to physical pixels — the swap chain must be at physical resolution for
        /// crisp rendering, and all injected input coordinates must use the same scale.
        /// <code>
        ///   panel.SizeChanged += (_, e) =>
        ///   {
        ///       float scale = (float)(panel.XamlRoot?.RasterizationScale ?? 1.0);
        ///       GodotWinUI3Embed.NotifyPanelResize(0,
        ///           (int)(e.NewSize.Width * scale), (int)(e.NewSize.Height * scale));
        ///   };
        /// </code>
        /// </remarks>
        /// <param name="windowId">Godot window ID.</param>
        /// <param name="width">New panel width in physical pixels (DIP × RasterizationScale).</param>
        /// <param name="height">New panel height in physical pixels (DIP × RasterizationScale).</param>
        public static void NotifyPanelResize(int windowId, int width, int height)
        {
            GodotWinUI3Native.godot_winui3_notify_panel_resize(windowId, width, height);
        }

        /// <summary>
        /// Sets the panel's composition scale (physical pixels per DIP) so the
        /// engine can invert it on the swap chain via SetMatrixTransform. Without
        /// this, a swap chain sized in physical pixels overflows past the panel
        /// bounds because <c>SwapChainPanel</c> maps one swap-chain pixel to one DIP.
        /// </summary>
        /// <remarks>
        /// Call once with the panel's initial <c>CompositionScaleX/Y</c> before
        /// <see cref="EngineStart"/>, then again from
        /// <c>SwapChainPanel.CompositionScaleChanged</c>. Pair with a fresh
        /// <see cref="NotifyPanelResize"/> so buffer size and transform stay in sync:
        /// <code>
        ///   panel.CompositionScaleChanged += (s, _) =>
        ///   {
        ///       GodotWinUI3Embed.SetCompositionScale(0,
        ///           s.CompositionScaleX, s.CompositionScaleY);
        ///       GodotWinUI3Embed.NotifyPanelResize(0,
        ///           (int)(panel.ActualWidth  * s.CompositionScaleX),
        ///           (int)(panel.ActualHeight * s.CompositionScaleY));
        ///   };
        /// </code>
        /// </remarks>
        /// <param name="windowId">Godot window ID (use 0 for the main window).</param>
        /// <param name="scaleX">Horizontal composition scale (e.g. 1.5 at 150% DPI).</param>
        /// <param name="scaleY">Vertical composition scale.</param>
        public static void SetCompositionScale(int windowId, float scaleX, float scaleY)
        {
            GodotWinUI3Native.godot_winui3_set_composition_scale(windowId, scaleX, scaleY);
        }

        /// <summary>
        /// Injects a mouse button press or release event into Godot.
        /// </summary>
        /// <remarks>
        /// Call from a <c>PointerPressed</c> / <c>PointerReleased</c> handler
        /// on the <c>SwapChainPanel</c>. Multiply coordinates by
        /// <c>XamlRoot.RasterizationScale</c> to convert from DIPs to physical pixels:
        /// <code>
        ///   panel.PointerPressed += (_, e) =>
        ///   {
        ///       var pt = e.GetCurrentPoint(panel);
        ///       float scale = (float)(panel.XamlRoot?.RasterizationScale ?? 1.0);
        ///       GodotWinUI3Embed.InjectMouseButton(
        ///           GodotMouseButton.Left, pressed: true,
        ///           (float)(pt.Position.X * scale), (float)(pt.Position.Y * scale));
        ///   };
        /// </code>
        /// </remarks>
        /// <param name="windowId">Godot window ID the event targets (use <c>0</c> for the main window).</param>
        /// <param name="button">Which mouse button (see <see cref="GodotMouseButton"/>).</param>
        /// <param name="pressed"><c>true</c> for press, <c>false</c> for release.</param>
        /// <param name="x">Panel-local X position in physical pixels (DIP × RasterizationScale).</param>
        /// <param name="y">Panel-local Y position in physical pixels (DIP × RasterizationScale).</param>
        public static void InjectMouseButton(int windowId, GodotMouseButton button, bool pressed, float x, float y)
        {
            GodotWinUI3Native.godot_winui3_inject_mouse_button(
                windowId, (int)button, pressed ? 1 : 0, x, y);
        }

        /// <summary>
        /// Injects a mouse motion event into Godot.
        /// </summary>
        /// <remarks>
        /// Call from a <c>PointerMoved</c> handler on the <c>SwapChainPanel</c>.
        /// All coordinates must be in physical pixels (multiply DIP values from
        /// <c>GetCurrentPoint</c> by <c>XamlRoot.RasterizationScale</c>).
        /// </remarks>
        /// <param name="windowId">Godot window ID the event targets (use <c>0</c> for the main window).</param>
        /// <param name="x">Current X position in physical pixels.</param>
        /// <param name="y">Current Y position in physical pixels.</param>
        /// <param name="relX">X delta since the previous motion event in physical pixels.</param>
        /// <param name="relY">Y delta since the previous motion event in physical pixels.</param>
        public static void InjectMouseMotion(int windowId, float x, float y, float relX, float relY)
        {
            GodotWinUI3Native.godot_winui3_inject_mouse_motion(windowId, x, y, relX, relY);
        }

        /// <summary>
        /// Injects a key press or release event into Godot.
        /// </summary>
        /// <remarks>
        /// Call from the <c>CoreWindow.KeyDown</c> / <c>KeyUp</c> events or
        /// from a <c>UIElement.KeyDown</c> / <c>KeyUp</c> override that
        /// forwards events to the panel:
        /// <code>
        ///   panel.KeyDown += (_, e) =>
        ///   {
        ///       GodotWinUI3Embed.InjectKey(
        ///           MapToGodotKey(e.Key), pressed: true,
        ///           echo: e.KeyStatus.WasKeyDown,
        ///           character: 0);
        ///   };
        /// </code>
        /// </remarks>
        /// <param name="windowId">Godot window ID the event targets (use <c>0</c> for the main window).</param>
        /// <param name="keycode">
        /// Godot <c>Key</c> enum value.  Map from
        /// <c>Windows.System.VirtualKey</c> using your own lookup table or
        /// Godot's <c>KeyMappingWindows</c> logic.
        /// </param>
        /// <param name="pressed"><c>true</c> for key-down, <c>false</c> for key-up.</param>
        /// <param name="echo">
        /// <c>true</c> if this is an auto-repeat (the key was already held down).
        /// Use <c>KeyRoutedEventArgs.KeyStatus.WasKeyDown</c>.
        /// </param>
        /// <param name="character">
        /// Unicode codepoint produced by this keystroke, or <c>0</c> if none.
        /// Obtain from a <c>CharacterReceived</c> handler and correlate by
        /// timestamp if needed.
        /// </param>
        public static void InjectKey(int windowId, int keycode, bool pressed, bool echo, uint character = 0)
        {
            GodotWinUI3Native.godot_winui3_inject_key(
                windowId, keycode, pressed ? 1 : 0, echo ? 1 : 0, character);
        }

        /// <summary>
        /// Injects a scroll-wheel event into Godot.
        /// </summary>
        /// <remarks>
        /// Call from a <c>PointerWheelChanged</c> handler on the <c>SwapChainPanel</c>:
        /// <code>
        ///   panel.PointerWheelChanged += (_, e) =>
        ///   {
        ///       var pt = e.GetCurrentPoint(panel);
        ///       int raw = pt.Properties.MouseWheelDelta;      // e.g. +120 or -120
        ///       bool isHorizontal = pt.Properties.IsHorizontalMouseWheel;
        ///       float notches = raw / 120.0f;
        ///       GodotWinUI3Embed.InjectMouseWheel(0,
        ///           (float)pt.Position.X, (float)pt.Position.Y,
        ///           deltaX: isHorizontal ?  notches : 0f,
        ///           deltaY: isHorizontal ? 0f : notches);
        ///   };
        /// </code>
        /// WinUI3 does NOT negate the vertical delta (positive = up), so pass it directly.
        /// </remarks>
        /// <param name="windowId">Godot window ID (use 0 for the main window).</param>
        /// <param name="x">Panel-local X position in pixels.</param>
        /// <param name="y">Panel-local Y position in pixels.</param>
        /// <param name="deltaX">Horizontal notches (positive = right).</param>
        /// <param name="deltaY">Vertical notches (positive = up / away from user).</param>
        public static void InjectMouseWheel(int windowId, float x, float y, float deltaX, float deltaY)
        {
            GodotWinUI3Native.godot_winui3_inject_mouse_wheel(windowId, x, y, deltaX, deltaY);
        }

        /// <summary>
        /// Sets the input routing mode for the embedded Godot window.
        /// </summary>
        /// <remarks>
        /// Call before the first rendered frame (after <see cref="EngineSetup"/>).
        /// <list type="bullet">
        ///   <item><see cref="GodotWinUI3InputMode.Native"/> (default) — Win32 delivers
        ///     mouse events directly to the child HWND via hit-testing. Only keyboard
        ///     needs explicit injection or a hook.</item>
        ///   <item><see cref="GodotWinUI3InputMode.Xaml"/> — Godot's WndProc ignores all
        ///     native mouse/keyboard messages. Wire up every event on the
        ///     <c>SwapChainPanel</c> and call the <c>Inject*</c> helpers.</item>
        /// </list>
        /// </remarks>
        public static void SetInputMode(GodotWinUI3InputMode mode)
        {
            GodotWinUI3Native.godot_winui3_set_input_mode((int)mode);
        }

        // ---------------------------------------------------------------
        // Host <-> Engine messaging
        //
        // GDScript side:
        //   func _ready():
        //       WinUI3Host.register_handler("set_device_info", _on_set_device_info)
        //       WinUI3Host.send_to_host("ready", [])
        //
        //   func _on_set_device_info(info: Dictionary) -> Dictionary:
        //       device_info = info
        //       return {"ok": true}
        //
        // C# side:
        //   GodotWinUI3Embed.SetHostMessageHandler((method, argsJson) => {
        //       if (method == "ready") { /* ... */; return null; }
        //       if (method == "device_info_changed") {
        //           var args = JsonDocument.Parse(argsJson);
        //           // ... handle ...
        //           return null;
        //       }
        //       return null;
        //   });
        //
        //   string? result = GodotWinUI3Embed.CallEngine(
        //       "set_device_info",
        //       /* args */ "[{\"vendor\":\"Foo\",\"model\":\"Bar\"}]");
        // ---------------------------------------------------------------

        // Pinned delegate — must outlive the native callback registration.
        private static GodotWinUI3Native.GodotHostMsgDelegate? _hostMsgDelegatePin;

        /// <summary>
        /// Handler invoked when GDScript runs <c>WinUI3Host.send_to_host(method, args)</c>.
        /// </summary>
        /// <param name="method">UTF-8 method name (e.g. "device_info_changed").</param>
        /// <param name="argsJson">JSON-encoded array of arguments. Always non-null; <c>"[]"</c> if empty.</param>
        /// <returns>JSON-encoded return value, or <c>null</c> for "no return" (Variant::NIL on the GDScript side).</returns>
        public delegate string? HostMessageHandler(string method, string argsJson);

        /// <summary>
        /// Installs the engine -> host message handler. Pass <c>null</c> to clear.
        /// Call BEFORE <see cref="EngineStart"/> so messages emitted during script
        /// <c>_ready</c> are not dropped. The handler is invoked on the engine
        /// iteration thread (your UI thread when driving with a DispatcherQueueTimer).
        /// </summary>
        public static void SetHostMessageHandler(HostMessageHandler? handler)
        {
            if (handler == null)
            {
                GodotWinUI3Native.godot_winui3_set_host_message_callback(null);
                _hostMsgDelegatePin = null;
                return;
            }

            _hostMsgDelegatePin = (methodPtr, argsPtr) =>
            {
                string method = Marshal.PtrToStringUTF8(methodPtr) ?? string.Empty;
                string args = Marshal.PtrToStringUTF8(argsPtr) ?? string.Empty;
                string? ret;
                try
                {
                    ret = handler(method, args);
                }
                catch (Exception ex)
                {
                    // Don't let managed exceptions cross the native boundary.
                    System.Diagnostics.Debug.WriteLine(
                        $"WinUI3 host handler '{method}' threw: {ex}");
                    ret = null;
                }
                if (ret != null)
                {
                    IntPtr retUtf8 = Utf8Alloc(ret);
                    try
                    {
                        GodotWinUI3Native.godot_winui3_set_call_return(retUtf8);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(retUtf8);
                    }
                }
            };
            GodotWinUI3Native.godot_winui3_set_host_message_callback(_hostMsgDelegatePin);
        }

        /// <summary>
        /// Invokes a GDScript handler registered via
        /// <c>WinUI3Host.register_handler(method, callable)</c>, also firing the
        /// <c>WinUI3Host.host_message_received</c> signal.
        /// </summary>
        /// <param name="method">UTF-8 method name to dispatch.</param>
        /// <param name="argsJson">
        /// JSON-encoded array of arguments, e.g. <c>"[42, \"hello\"]"</c>.
        /// May be <c>null</c> or empty for no arguments.
        /// </param>
        /// <returns>
        /// JSON-encoded return value, or <c>null</c> if the handler returned
        /// nothing or no handler is registered for <paramref name="method"/>.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the engine bridge is not yet initialised
        /// (call <see cref="EngineSetup"/> first).
        /// </exception>
        public static string? CallEngine(string method, string? argsJson = null)
        {
            if (string.IsNullOrEmpty(method))
                throw new ArgumentException("method must be non-empty.", nameof(method));

            IntPtr methodUtf8 = Utf8Alloc(method);
            IntPtr argsUtf8 = argsJson != null ? Utf8Alloc(argsJson) : IntPtr.Zero;
            IntPtr retUtf8 = IntPtr.Zero;
            try
            {
                int ok = GodotWinUI3Native.godot_winui3_call_engine(methodUtf8, argsUtf8, out retUtf8);
                if (ok == 0)
                {
                    throw new InvalidOperationException(
                        "WinUI3Host bridge is not initialised. Call EngineSetup() first.");
                }
                if (retUtf8 == IntPtr.Zero)
                {
                    return null;
                }
                return Marshal.PtrToStringUTF8(retUtf8);
            }
            finally
            {
                if (retUtf8 != IntPtr.Zero)
                {
                    GodotWinUI3Native.godot_winui3_free_string(retUtf8);
                }
                if (argsUtf8 != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(argsUtf8);
                }
                Marshal.FreeHGlobal(methodUtf8);
            }
        }

        // Internal helper: allocate a null-terminated UTF-8 buffer with HGlobal.
        // Caller must free with Marshal.FreeHGlobal.
        private static IntPtr Utf8Alloc(string s)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s + '\0');
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        // ---------------------------------------------------------------
        // Typed wrappers around the host <-> engine bridge.
        //
        // IGodotWinUI3Sender — host -> engine (outbound). The host implements
        //   or uses the default Sender to push responses, errors and config
        //   updates into the engine via CallEngine.
        //
        // IGodotWinUI3Receiver — engine -> host (inbound). The host implements
        //   this and registers it via SetReceiver. Messages from
        //   WinUI3Host.send_to_host(method, args) are routed by method-name
        //   prefix:
        //     - "get_*"  -> OnGetString (return value forwarded to GDScript)
        //     - anything else -> OnRequest (fire-and-forget)
        // ---------------------------------------------------------------

        /// <summary>
        /// Outbound messages from the WinUI3 host into the embedded Godot engine.
        /// All methods are thin wrappers around <see cref="CallEngine"/>; provide
        /// your own implementation if you need to intercept, queue or marshal
        /// the calls onto a specific thread.
        /// </summary>
        public interface IGodotWinUI3Sender
        {
            /// <summary>Sends a successful response payload for <paramref name="method"/>.</summary>
            /// <param name="method">GDScript-side method registered with WinUI3Host.register_handler.</param>
            /// <param name="responseJson">JSON-encoded array of arguments (e.g. <c>"[{\"ok\":true}]"</c>).</param>
            void SendResponse(string method, string responseJson);

            /// <summary>Sends an error payload for <paramref name="method"/>.</summary>
            /// <param name="method">GDScript-side method registered with WinUI3Host.register_handler.</param>
            /// <param name="errorMessage">Human-readable error string. Wrapped as a single-element JSON array.</param>
            void SendError(string method, string errorMessage);

            /// <summary>
            /// Notifies the engine that host-side configuration changed.
            /// Invokes the GDScript handler registered for <c>"configuration_changed"</c>.
            /// </summary>
            /// <param name="configJson">JSON-encoded array of arguments describing the new configuration.</param>
            void SendConfigurationChanged(string configJson);
        }

        /// <summary>
        /// Inbound messages from the embedded Godot engine into the WinUI3 host.
        /// Implement this and register it via <see cref="SetReceiver"/>.
        /// Routing is by method-name prefix (see remarks on <see cref="SetReceiver"/>).
        /// </summary>
        public interface IGodotWinUI3Receiver
        {
            /// <summary>
            /// Called for any fire-and-forget message from GDScript whose method
            /// name does NOT start with <c>"get_"</c>.
            /// </summary>
            /// <param name="method">UTF-8 method name (e.g. <c>"device_info_changed"</c>).</param>
            /// <param name="argsJson">JSON-encoded array of arguments. Always non-null; <c>"[]"</c> if empty.</param>
            void OnRequest(string method, string argsJson);

            /// <summary>
            /// Called when GDScript invokes a method whose name starts with <c>"get_"</c>.
            /// The returned string is forwarded back as the call's return value.
            /// </summary>
            /// <param name="method">UTF-8 method name (e.g. <c>"get_device_info"</c>).</param>
            /// <param name="argsJson">JSON-encoded array of arguments. Always non-null; <c>"[]"</c> if empty.</param>
            /// <returns>JSON-encoded return value, or <c>null</c> if not handled.</returns>
            string? OnGetString(string method, string argsJson);
        }

        /// <summary>
        /// Default <see cref="IGodotWinUI3Sender"/> backed by <see cref="CallEngine"/>.
        /// Stateless — safe to share a single instance across the app.
        /// </summary>
        public sealed class GodotWinUI3Sender : IGodotWinUI3Sender
        {
            public void SendResponse(string method, string responseJson)
            {
                CallEngine(method, responseJson);
            }

            public void SendError(string method, string errorMessage)
            {
                string escaped = System.Text.Json.JsonSerializer.Serialize(errorMessage);
                CallEngine(method, "[" + escaped + "]");
            }

            public void SendConfigurationChanged(string configJson)
            {
                CallEngine("configuration_changed", configJson);
            }
        }

        /// <summary>
        /// Default sender instance — a thin wrapper around <see cref="CallEngine"/>.
        /// </summary>
        public static IGodotWinUI3Sender Sender { get; } = new GodotWinUI3Sender();

        /// <summary>
        /// Routes inbound engine-&gt;host messages to <paramref name="receiver"/>.
        /// Methods whose name starts with <c>"get_"</c> are dispatched to
        /// <see cref="IGodotWinUI3Receiver.OnGetString"/> and the returned string
        /// is forwarded back to GDScript; all other methods are dispatched to
        /// <see cref="IGodotWinUI3Receiver.OnRequest"/> with no return value.
        /// Pass <c>null</c> to clear. Internally installs a host-message handler
        /// via <see cref="SetHostMessageHandler"/>, replacing any previous one.
        /// </summary>
        public static void SetReceiver(IGodotWinUI3Receiver? receiver)
        {
            if (receiver == null)
            {
                SetHostMessageHandler(null);
                return;
            }

            SetHostMessageHandler((method, argsJson) =>
            {
                if (method.StartsWith("get_", StringComparison.Ordinal))
                {
                    return receiver.OnGetString(method, argsJson);
                }
                receiver.OnRequest(method, argsJson);
                return null;
            });
        }
    }
}
