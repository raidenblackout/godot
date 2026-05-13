// MapViewPage.xaml.cs
// Hosts the embedded Godot engine inside a SwapChainPanel, wires up the
// host<->engine bridge, and answers `request_data` calls from GDScript by
// returning indoor-map / rooms JSON loaded from the bundled Assets folder.

namespace GodotWinUI3Sample.Views;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot.WinUI3;
using GodotWinUI3Sample.Interop;
using GodotWinUI3Sample.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

public sealed partial class MapViewPage : Page
{
	// Default to the MapViewProject folder already in the Godot repo. Override
	// in code or with a sibling-of-exe folder named "MapViewProject" if the
	// repo path is missing at runtime.
	private const string DefaultProjectPath = @"C:\Projects\godot\MapViewProject";

	private readonly MapViewModel _viewModel = new();

	// Singletons keep the call sites in the bridge wrappers compact and let
	// the engine and receiver be reused if we ever navigate away and back.
	private HostInteropEngine Engine => HostInteropEngine.Instance!;
	private HostInteropReceiver Receiver => HostInteropReceiver.Instance!;
	private HostInteropSender Sender => HostInteropSender.Instance!;

	private DispatcherQueueTimer? _tickTimer;
	private double _lastX, _lastY;

	public MapViewPage()
	{
		InitializeComponent();
		NavigationCacheMode = NavigationCacheMode.Required;

		// Construct singletons on the UI thread so the sender/receiver capture
		// the dispatcher's SynchronizationContext.
		_ = new HostInteropEngine();
		_ = new HostInteropReceiver();
		_ = new HostInteropSender();
	}

	private void OnPanelLoaded(object sender, RoutedEventArgs e)
	{
		if (Engine.IsRunning) return;

		var hostHwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
		var panelPtr = Marshal.GetComInterfaceForObject(GodotPanel, typeof(ISwapChainPanelNative));

		Engine.ProjectPath = ResolveProjectPath();
		if (!Engine.Initialize(hostHwnd, GodotPanel, panelPtr))
		{
			Debug.WriteLine("[MapViewPage] Engine initialisation failed.");
			return;
		}

		Receiver.Initialize();
		Receiver.OnDataCommand += OnDataCommand;
		Receiver.OnUIControlCommand += OnUIControlCommand;
		Receiver.OnRendererStatus += OnRendererStatus;
		Receiver.OnUnhandledMessage += OnUnhandledMessage;

		ConfigurePanel();

		if (!Engine.Start())
		{
			Debug.WriteLine("[MapViewPage] Engine start failed.");
			return;
		}

		_tickTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
		_tickTimer.Interval = TimeSpan.FromMilliseconds(16);
		_tickTimer.Tick += OnTick;
		_tickTimer.Start();
	}

	private void OnTick(DispatcherQueueTimer t, object e)
	{
		if (!Engine.IsRunning)
		{
			_tickTimer?.Stop();
			return;
		}

		if (Engine.Iterate())
		{
			_tickTimer?.Stop();
			Engine.Shutdown();
		}
	}

	// ---------------------------------------------------------------------
	// Engine -> Host
	// ---------------------------------------------------------------------

	private void OnDataCommand(object? sender, HostInteropMessageEventArgs e)
	{
		// args := ["st_data", "<sub_cmd>", "<payload>"]
		string[]? request;
		try
		{
			request = JsonSerializer.Deserialize<string[]>(e.ArgsJson);
		}
		catch (JsonException ex)
		{
			Debug.WriteLine($"[MapViewPage] OnDataCommand JSON parse failed: {ex.Message}");
			return;
		}

		if (request is null || request.Length < 2) return;
		var subCmd = request[1];

		switch (subCmd)
		{
			case "get_indoor_map":
				Sender.PostDataCommand("result_" + subCmd, _viewModel.GetIndoorMap());
				break;
			case "get_rooms":
				Sender.PostDataCommand("result_" + subCmd, _viewModel.GetRooms());
				break;
			case "get_scenes":
				Sender.PostDataCommand("result_" + subCmd, _viewModel.GetScenes());
				break;
			case "get_devices":
				Sender.PostDataCommand("result_" + subCmd, _viewModel.GetDevices());
				break;
			case "get_locations":
				Sender.PostDataCommand("result_" + subCmd, _viewModel.GetLocations());
				break;
			//// SimulatedResponse maps both names to the same JSON file
			//// (Resources/TestResource/DataSet_1/result_capability_status.json);
			//// mirror that here so device-status and bubble-status callers both
			//// get the same payload.
			//case "capability_status":
			//case "device_bubble_status":
			//	Sender.PostDataCommand("result_" + subCmd, _viewModel.GetCapabilityStatus());
			//	break;
			default:
				// Stay silent for unknown sub-commands. The GDScript
				// WindowsWinUI3Interactor will fall back to SimulatedResponse
				// after a short timeout if we don't reply.
				Debug.WriteLine($"[MapViewPage] No host data for sub-command '{subCmd}' (deferring to SimulatedResponse).");
				break;
		}
	}

	private void OnUIControlCommand(object? sender, HostInteropMessageEventArgs e)
	{
		Debug.WriteLine($"[MapViewPage] UI: {e.Method} {e.ArgsJson}");
	}

	private void OnRendererStatus(object? sender, HostInteropMessageEventArgs e)
	{
		Debug.WriteLine($"[MapViewPage] Renderer: {e.Method} {e.ArgsJson}");
	}

	private void OnUnhandledMessage(object? sender, HostInteropMessageEventArgs e)
	{
		Debug.WriteLine($"[MapViewPage] Unhandled: {e.Method} {e.ArgsJson}");
	}

	// ---------------------------------------------------------------------
	// Panel sizing / DPI
	// ---------------------------------------------------------------------

	private void ConfigurePanel()
	{
		float scaleX = GodotPanel.CompositionScaleX;
		float scaleY = GodotPanel.CompositionScaleY;
		double width = GodotPanel.ActualWidth * scaleX;
		double height = GodotPanel.ActualHeight * scaleY;
		Engine.ConfigurePanel(width, height, scaleX, scaleY);
	}

	private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) => ConfigurePanel();

	private void OnPanelCompositionScaleChanged(SwapChainPanel sender, object args) => ConfigurePanel();

	// ---------------------------------------------------------------------
	// Input forwarding (physical pixels = DIP * CompositionScale)
	// ---------------------------------------------------------------------

	private float DpiScale => GodotPanel.CompositionScaleX;

	private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		var pt = e.GetCurrentPoint(GodotPanel);
		float scale = DpiScale;
		float x = (float)(pt.Position.X * scale), y = (float)(pt.Position.Y * scale);
		if (pt.Properties.IsLeftButtonPressed) Engine.InjectMouseButton(GodotMouseButton.Left, true, x, y);
		if (pt.Properties.IsRightButtonPressed) Engine.InjectMouseButton(GodotMouseButton.Right, true, x, y);
		if (pt.Properties.IsMiddleButtonPressed) Engine.InjectMouseButton(GodotMouseButton.Middle, true, x, y);
		_ = GodotPanel.Focus(FocusState.Programmatic);
		e.Handled = true;
	}

	private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		var pt = e.GetCurrentPoint(GodotPanel);
		float scale = DpiScale;
		float x = (float)(pt.Position.X * scale), y = (float)(pt.Position.Y * scale);
		if (!pt.Properties.IsLeftButtonPressed) Engine.InjectMouseButton(GodotMouseButton.Left, false, x, y);
		if (!pt.Properties.IsRightButtonPressed) Engine.InjectMouseButton(GodotMouseButton.Right, false, x, y);
		if (!pt.Properties.IsMiddleButtonPressed) Engine.InjectMouseButton(GodotMouseButton.Middle, false, x, y);
		_ = GodotPanel.Focus(FocusState.Programmatic);
		e.Handled = true;
	}

	private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
	{
		var pt = e.GetCurrentPoint(GodotPanel);
		float scale = DpiScale;
		float px = (float)(pt.Position.X * scale);
		float py = (float)(pt.Position.Y * scale);
		Engine.InjectMouseMotion(px, py,
			(float)((pt.Position.X - _lastX) * scale),
			(float)((pt.Position.Y - _lastY) * scale));
		_lastX = pt.Position.X;
		_lastY = pt.Position.Y;
		e.Handled = true;
	}

	private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
	{
		var pt = e.GetCurrentPoint(GodotPanel);
		float scale = DpiScale;
		float x = (float)(pt.Position.X * scale), y = (float)(pt.Position.Y * scale);
		float notches = pt.Properties.MouseWheelDelta / 120.0f;
		if (pt.Properties.IsHorizontalMouseWheel)
			Engine.InjectMouseWheel(x, y, deltaX: notches, deltaY: 0f);
		else
			Engine.InjectMouseWheel(x, y, deltaX: 0f, deltaY: notches);
		e.Handled = true;
	}

	private void OnKeyDown(object sender, KeyRoutedEventArgs e)
	{
		Engine.InjectKey((int)e.Key, pressed: true, echo: e.KeyStatus.WasKeyDown, character: 0);
	}

	private void OnKeyUp(object sender, KeyRoutedEventArgs e)
	{
		Engine.InjectKey((int)e.Key, pressed: false, echo: false, character: 0);
	}

	// ---------------------------------------------------------------------
	// Project path resolution
	// ---------------------------------------------------------------------

	private static string ResolveProjectPath()
	{
		// Prefer the TestProject.pck copied next to the exe by the csproj
		// (production-style deployment). Fall back to the dev path.
		var sibling = Path.Combine(AppContext.BaseDirectory, "Assets", "TestProject.pck");
		if (File.Exists(sibling)) return sibling;
		var siblingFlat = Path.Combine(AppContext.BaseDirectory, "TestProject.pck");
		if (File.Exists(siblingFlat)) return siblingFlat;
		return DefaultProjectPath;
	}
}
