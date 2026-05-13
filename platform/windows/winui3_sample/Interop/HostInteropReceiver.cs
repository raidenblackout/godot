namespace GodotWinUI3Sample.Interop;

using Godot.WinUI3;
// HostInteropReceiver.cs
// Wrapper class for receiving messages from the Godot engine via GodotWinUIHostInterop.
//
// This file is part of SmartThings MapView integration with Godot engine.
// Provides a structured interface for handling messages sent from GDScript via WinUI3Host.send_to_host().

using System;
using System.Text.Json;
using System.Threading;
/// <summary>
/// Defines command categories for message routing.
/// </summary>
public enum CommandCategory
{
	/// <summary>Unknown or unclassified command.</summary>
	Unknown,

	/// <summary>UI control command (dialogs, navigation, views).</summary>
	UI,

	/// <summary>Dialog-specific command.</summary>
	Dialog,

	/// <summary>SmartThings data command (device status, events).</summary>
	Data,

	/// <summary>Renderer status command.</summary>
	Renderer
}

/// <summary>
/// Provides data for host interop message events.
/// </summary>
public sealed class HostInteropMessageEventArgs : EventArgs
{
	/// <summary>
	/// Gets the method name (command) sent from GDScript.
	/// </summary>
	public required string Method { get; init; }

	/// <summary>
	/// Gets the JSON-encoded array of arguments.
	/// </summary>
	public required string ArgsJson { get; init; }

	/// <summary>
	/// Gets the timestamp when the message was received.
	/// </summary>
	public DateTime Timestamp { get; init; }

	/// <summary>
	/// Gets the command category for routing.
	/// </summary>
	public CommandCategory Category { get; internal set; }

	/// <summary>
	/// Gets or sets the response to send back to the Godot engine.
	/// Set this value in your event handler to return data to GDScript.
	/// </summary>
	public string? Response { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the message was handled.
	/// </summary>
	public bool Handled { get; set; }

	/// <summary>
	/// Deserializes the arguments JSON to the specified type.
	/// </summary>
	/// <typeparam name="T">The type to deserialize to.</typeparam>
	/// <returns>The deserialized object, or default value if deserialization fails.</returns>
	public T? GetArgsAs<T>()
	{
		try
		{
			return JsonSerializer.Deserialize<T>(ArgsJson);
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropMessageEventArgs] Deserialization error: {ex.Message}");
			return default;
		}
	}
}

/// <summary>
/// Receives and dispatches messages from the Godot engine to the WinUI3 host application.
/// This class wraps the <see cref="GodotWinUI3Embed.SetHostMessageHandler"/> functionality
/// and provides a structured command handling pattern similar to DotNetBridge.
/// </summary>
/// <remarks>
/// <para>
/// Messages are received from GDScript via <c>WinUI3Host.send_to_host(method, args)</c> calls.
/// The receiver dispatches these messages to appropriate handlers based on the command category.
/// </para>
/// <para>
/// Command categories:
/// <list type="bullet">
///   <item><description><c>UI</c> - UI control commands (dialogs, navigation, views)</description></item>
///   <item><description><c>Data</c> - SmartThings data commands (device status, events)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class HostInteropReceiver : IDisposable
{
	private readonly SynchronizationContext? _synchronizationContext;
	private readonly JsonSerializerOptions _jsonOptions;
	private bool _isInitialized;
	private bool _isDisposed;

	/// <summary>
	/// Gets the singleton instance of the receiver.
	/// </summary>
	public static HostInteropReceiver? Instance { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="HostInteropReceiver"/> class.
	/// </summary>
	public HostInteropReceiver()
	{
		_synchronizationContext = SynchronizationContext.Current;
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};

		Instance = this;
	}

	/// <summary>
	/// Synchronous provider for <c>get_devices</c> calls from GDScript.
	/// Set this from host code (e.g. MapViewPage) to inject a JSON-serializable
	/// payload — the dispatcher invokes it on the engine/UI thread and returns
	/// the JSON string straight back to <c>WinUI3Host.send_to_host(...)</c> on
	/// the engine side. Default returns a small mock list so the round-trip is
	/// visible even before the host wires up real device enumeration.
	/// </summary>
	public Func<string>? GetDevicesProvider { get; set; }

	#region Events

	/// <summary>
	/// Occurs when a UI control command is received from the Godot engine.
	/// </summary>
	public event EventHandler<HostInteropMessageEventArgs>? OnUIControlCommand;

	/// <summary>
	/// Occurs when a dialog control command is received from the Godot engine.
	/// </summary>
	public event EventHandler<HostInteropMessageEventArgs>? OnDialogControlCommand;

	/// <summary>
	/// Occurs when a SmartThings data command is received from the Godot engine.
	/// </summary>
	public event EventHandler<HostInteropMessageEventArgs>? OnDataCommand;

	/// <summary>
	/// Occurs when a renderer status message is received from the Godot engine.
	/// </summary>
	public event EventHandler<HostInteropMessageEventArgs>? OnRendererStatus;

	/// <summary>
	/// Occurs when an unhandled message is received from the Godot engine.
	/// </summary>
	public event EventHandler<HostInteropMessageEventArgs>? OnUnhandledMessage;

	#endregion

	#region Public Methods

	/// <summary>
	/// Initializes the receiver and registers the message handler with the Godot engine.
	/// Call this method before <see cref="GodotWinUI3Embed.EngineStart"/> to ensure
	/// messages emitted during script <c>_ready</c> are not dropped.
	/// </summary>
	/// <returns><c>true</c> if initialization succeeded; otherwise, <c>false</c>.</returns>
	public bool Initialize()
	{
		if (_isInitialized)
		{
			System.Diagnostics.Debug.WriteLine("[HostInteropReceiver] Already initialized.");
			return true;
		}

		try
		{
			GodotWinUI3Embed.SetHostMessageHandler(HandleHostMessage);
			_isInitialized = true;
			System.Diagnostics.Debug.WriteLine("[HostInteropReceiver] Initialized successfully.");
			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropReceiver] Initialization failed: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Shuts down the receiver and unregisters the message handler.
	/// </summary>
	public void Shutdown()
	{
		if (!_isInitialized)
		{
			return;
		}

		try
		{
			GodotWinUI3Embed.SetHostMessageHandler(null);
			_isInitialized = false;
			System.Diagnostics.Debug.WriteLine("[HostInteropReceiver] Shutdown completed.");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropReceiver] Shutdown error: {ex.Message}");
		}
	}

	#endregion

	#region Private Methods

	/// <summary>
	/// Handles incoming messages from the Godot engine.
	/// This method is called on the engine iteration thread.
	/// </summary>
	/// <param name="method">The method name (command) sent from GDScript.</param>
	/// <param name="argsJson">JSON-encoded array of arguments.</param>
	/// <returns>JSON-encoded return value, or <c>null</c> for no return.</returns>
	private string? HandleHostMessage(string method, string argsJson)
	{
		// System.Diagnostics.Debug.WriteLine($"[HostInteropReceiver] Received: method={method}, args={argsJson}");

		try
		{
			// Synchronous fast-path for request/response-style calls. The
			// default category dispatchers use SynchronizationContext.Post —
			// fire-and-forget — so a handler running there can't populate the
			// return value in time. Anything that needs a synchronous reply
			// to the GDScript caller is handled here before the async fan-out.
			if (method == "get_devices")
			{
				return GetDevicesResponse();
			}

			// Parse the command to determine routing
			var commandInfo = ParseCommand(method, argsJson);

			// Dispatch based on command category
			return commandInfo.Category switch
			{
				CommandCategory.UI => DispatchUICommand(commandInfo),
				CommandCategory.Dialog => DispatchDialogCommand(commandInfo),
				CommandCategory.Data => DispatchDataCommand(commandInfo),
				CommandCategory.Renderer => DispatchRendererCommand(commandInfo),
				_ => DispatchUnhandledCommand(commandInfo)
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropReceiver] Error handling message: {ex.Message}");
			return SerializeError(ex.Message);
		}
	}

	/// <summary>
	/// Parses a command method name and arguments into structured command information.
	/// </summary>
	private static HostInteropCommandInfo ParseCommand(string method, string argsJson)
	{
		var commandInfo = new HostInteropCommandInfo
		{
			Method = method,
			ArgsJson = argsJson,
			Category = DetermineCategory(method)
		};

		return commandInfo;
	}

	/// <summary>
	/// Determines the command category based on the method name.
	/// </summary>
	private static CommandCategory DetermineCategory(string method)
	{
		// Dialog-related commands
		if (method.Contains("dialog"))
		{
			return CommandCategory.Dialog;
		}

		// Renderer-related commands
		if (method.StartsWith("renderer_") || method.StartsWith("notify_renderer"))
		{
			return CommandCategory.Renderer;
		}

		// UI-related commands
		if (method.StartsWith("show_") || method.StartsWith("hide_") ||
			method.StartsWith("launch_") ||
			method.StartsWith("update_") || method.Contains("navigation"))
		{
			return CommandCategory.UI;
		}

		// Data-related commands
		if (method.StartsWith("request_data"))
		{
			return CommandCategory.Data;
		}

		return CommandCategory.Unknown;
	}

	/// <summary>
	/// Dispatches a UI command to registered handlers.
	/// </summary>
	private string? DispatchUICommand(HostInteropCommandInfo command)
	{
		var eventArgs = CreateEventArgs(command);

		_synchronizationContext?.Post(_ =>
		{
			OnUIControlCommand?.Invoke(this, eventArgs);
		}, null);

		return eventArgs.Response;
	}

	/// <summary>
	/// Dispatches a dialog command to registered handlers.
	/// </summary>
	private string? DispatchDialogCommand(HostInteropCommandInfo command)
	{
		var eventArgs = CreateEventArgs(command);

		_synchronizationContext?.Post(_ =>
		{
			OnDialogControlCommand?.Invoke(this, eventArgs);
		}, null);

		return eventArgs.Response;
	}

	/// <summary>
	/// Dispatches a data command to registered handlers.
	/// </summary>
	private string? DispatchDataCommand(HostInteropCommandInfo command)
	{
		var eventArgs = CreateEventArgs(command);

		_synchronizationContext?.Post(_ =>
		{
			OnDataCommand?.Invoke(this, eventArgs);
		}, null);

		return eventArgs.Response;
	}

	/// <summary>
	/// Dispatches a renderer command to registered handlers.
	/// </summary>
	private string? DispatchRendererCommand(HostInteropCommandInfo command)
	{
		var eventArgs = CreateEventArgs(command);

		_synchronizationContext?.Post(_ =>
		{
			OnRendererStatus?.Invoke(this, eventArgs);
		}, null);

		return eventArgs.Response;
	}

	/// <summary>
	/// Dispatches an unhandled command to registered handlers.
	/// </summary>
	private string? DispatchUnhandledCommand(HostInteropCommandInfo command)
	{
		var eventArgs = CreateEventArgs(command);

		_synchronizationContext?.Post(_ =>
		{
			OnUnhandledMessage?.Invoke(this, eventArgs);
		}, null);

		return eventArgs.Response;
	}

	/// <summary>
	/// Creates event arguments from command information.
	/// </summary>
	private static HostInteropMessageEventArgs CreateEventArgs(HostInteropCommandInfo command)
	{
		return new HostInteropMessageEventArgs
		{
			Method = command.Method,
			ArgsJson = command.ArgsJson,
			Category = command.Category,
			Timestamp = DateTime.UtcNow
		};
	}

	/// <summary>
	/// Builds the JSON response for a <c>get_devices</c> call. Uses the
	/// host-supplied <see cref="GetDevicesProvider"/> if one is registered;
	/// otherwise emits a small mock list so the round-trip can be exercised
	/// before real device enumeration is wired up.
	/// </summary>
	private string GetDevicesResponse()
	{
		if (GetDevicesProvider != null)
		{
			try
			{
				return GetDevicesProvider() ?? "[]";
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[HostInteropReceiver] GetDevicesProvider threw: {ex.Message}");
				return SerializeError(ex.Message) ?? "[]";
			}
		}

		var mock = new object[]
		{
			new { id = "dev-1", name = "Living Room Light", type = "switch", on = true },
			new { id = "dev-2", name = "Kitchen Temperature", type = "sensor", value = 22.5, unit = "C" },
			new { id = "dev-3", name = "Front Door Lock", type = "lock", locked = true },
			new { id = "dev-4", name = "Bedroom Hue", type = "color_light", on = false, color = "#ff9933" },
			new { host = Environment.MachineName, user = Environment.UserName, time = DateTime.UtcNow.ToString("o") },
		};
		return JsonSerializer.Serialize(mock, _jsonOptions);
	}

	/// <summary>
	/// Serializes an error message to JSON.
	/// </summary>
	private static string? SerializeError(string message)
	{
		return $"{{\"error\":\"{message.Replace("\"", "\\\"")}\"}}";
	}

	#endregion

	#region IDisposable

	/// <summary>
	/// Releases all resources used by the <see cref="HostInteropReceiver"/>.
	/// </summary>
	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		Shutdown();
		Instance = null;
		_isDisposed = true;
		GC.SuppressFinalize(this);
	}

	#endregion

	#region Nested Types

	/// <summary>
	/// Represents parsed command information for internal processing.
	/// </summary>
	private sealed class HostInteropCommandInfo
	{
		public required string Method { get; init; }
		public required string ArgsJson { get; init; }
		public CommandCategory Category { get; init; }
	}

	#endregion
}
