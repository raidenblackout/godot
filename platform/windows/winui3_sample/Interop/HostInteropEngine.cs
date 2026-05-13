// HostInteropEngine.cs
// Lifecycle wrapper around GodotWinUI3Embed: brings up the embedded Godot
// engine inside a WinUI3 SwapChainPanel, drives its main loop, and forwards
// pointer/scale events.
//
// Construct once on the UI thread; the singleton is reachable via
// HostInteropEngine.Instance.

namespace GodotWinUI3Sample.Interop;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Godot.WinUI3;
using Microsoft.UI.Xaml.Controls;

public sealed class HostInteropEngine : IDisposable
{
	public static HostInteropEngine? Instance { get; private set; }

	public string ProjectPath { get; set; } = string.Empty;
	public string RenderingDriver { get; set; } = "d3d12";
	public bool IsRunning { get; private set; }

	private bool _isSetup;
	private bool _isDisposed;

	// File sink for engine logs. The log callback can fire from any engine
	// thread (print/error handlers are global), so all writes are serialised
	// through _logFileLock. AutoFlush keeps the file usable for live tailing
	// while the sample is running.
	private static readonly object _logFileLock = new();
	private static StreamWriter? _logFileWriter;
	private static string? _logFilePath;

	public HostInteropEngine()
	{
		Instance = this;
	}

	public static string? LogFilePath => _logFilePath;

	/// <summary>
	/// Initialises the embedded engine. Order matters:
	///   1. Log callback (so setup-time errors are captured).
	///   2. Parent HWND (consumed by DisplayServer during setup2).
	///   3. EngineSetup (Main::setup).
	///   4. SwapChainPanel (engine AddRefs; caller releases).
	/// </summary>
	public bool Initialize(IntPtr hostHwnd, SwapChainPanel panel, IntPtr panelComPtr)
	{
		if (string.IsNullOrWhiteSpace(ProjectPath))
		{
			throw new InvalidOperationException("ProjectPath must be set before Initialize().");
		}

		OpenLogFile();
		GodotWinUI3Embed.SetLogCallback(OnGodotLog);
		GodotWinUI3Embed.SetEmbeddedParentHwnd(hostHwnd);

		string[] args = { "godot", "--path", ProjectPath, "--rendering-driver", RenderingDriver };
		if (!GodotWinUI3Embed.EngineSetup(args))
		{
			Debug.WriteLine("[HostInteropEngine] EngineSetup failed.");
			return false;
		}
		_isSetup = true;

		try
		{
			GodotWinUI3Embed.SetSwapChainPanel(0, panelComPtr);
		}
		finally
		{
			// Engine AddRefs internally; release the reference obtained via
			// Marshal.GetComInterfaceForObject() on the caller side.
			if (panelComPtr != IntPtr.Zero)
			{
				Marshal.Release(panelComPtr);
			}
		}

		return true;
	}

	public bool Start()
	{
		if (!_isSetup)
		{
			Debug.WriteLine("[HostInteropEngine] Start() called before Initialize().");
			return false;
		}
		IsRunning = GodotWinUI3Embed.EngineStart();
		if (!IsRunning)
		{
			Debug.WriteLine("[HostInteropEngine] EngineStart failed.");
		}
		return IsRunning;
	}

	/// <summary>Returns true when the engine wants to quit.</summary>
	public bool Iterate()
	{
		if (!IsRunning) return true;
		return GodotWinUI3Embed.EngineIteration();
	}

	public void Shutdown()
	{
		if (!_isSetup) return;
		GodotWinUI3Embed.EngineShutdown();
		IsRunning = false;
		_isSetup = false;
	}

	public void ConfigurePanel(double widthPx, double heightPx, float scaleX, float scaleY)
	{
		GodotWinUI3Embed.SetCompositionScale(0, scaleX, scaleY);
		GodotWinUI3Embed.NotifyPanelResize(0, (int)widthPx, (int)heightPx);
	}

	public void InjectMouseButton(GodotMouseButton button, bool pressed, float x, float y)
		=> GodotWinUI3Embed.InjectMouseButton(0, button, pressed, x, y);

	public void InjectMouseMotion(float x, float y, float relX, float relY)
		=> GodotWinUI3Embed.InjectMouseMotion(0, x, y, relX, relY);

	public void InjectMouseWheel(float x, float y, float deltaX, float deltaY)
		=> GodotWinUI3Embed.InjectMouseWheel(0, x, y, deltaX, deltaY);

	public void InjectKey(int keycode, bool pressed, bool echo, uint character = 0)
		=> GodotWinUI3Embed.InjectKey(0, keycode, pressed, echo, character);

	public void SetInputMode(GodotWinUI3InputMode mode)
		=> GodotWinUI3Embed.SetInputMode(mode);

	private static void OnGodotLog(string message, GodotLogLevel level)
	{
		string tag = level switch
		{
			GodotLogLevel.Error => "Error",
			GodotLogLevel.Warning => "Warn",
			_ => "Print",
		};
		string line = $"[Godot/{tag}] {message}";
		Debug.WriteLine(line);
		WriteLogLine(tag, message);
	}

	private static void OpenLogFile()
	{
		try
		{
			string dir = Path.Combine(AppContext.BaseDirectory, "Logs");
			Directory.CreateDirectory(dir);
			string fileName = $"godot_{DateTime.Now:yyyyMMdd_HHmmss}.log";
			string path = Path.Combine(dir, fileName);

			lock (_logFileLock)
			{
				_logFileWriter?.Dispose();
				_logFileWriter = new StreamWriter(
					new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read),
					new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
				{
					AutoFlush = true,
				};
				_logFilePath = path;
				_logFileWriter.WriteLine($"=== Godot WinUI3 sample log opened {DateTime.Now:O} ===");
			}

			Debug.WriteLine($"[HostInteropEngine] Engine log file: {path}");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[HostInteropEngine] Failed to open log file: {ex.Message}");
		}
	}

	private static void WriteLogLine(string tag, string message)
	{
		try
		{
			lock (_logFileLock)
			{
				if (_logFileWriter == null) return;
				_logFileWriter.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{tag,-5}] {message}");
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[HostInteropEngine] Log write failed: {ex.Message}");
		}
	}

	private static void CloseLogFile()
	{
		lock (_logFileLock)
		{
			if (_logFileWriter == null) return;
			try
			{
				_logFileWriter.WriteLine($"=== Log closed {DateTime.Now:O} ===");
				_logFileWriter.Dispose();
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[HostInteropEngine] Log close failed: {ex.Message}");
			}
			_logFileWriter = null;
		}
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		Shutdown();
		CloseLogFile();
		if (Instance == this) Instance = null;
		_isDisposed = true;
		GC.SuppressFinalize(this);
	}
}
