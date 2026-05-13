namespace GodotWinUI3Sample.Interop;

using Godot.WinUI3;

// HostInteropSender.cs
// Wrapper class for sending messages to the Godot engine via GodotWinUIHostInterop.
//
// This file is part of SmartThings MapView integration with Godot engine.
// Provides a structured interface for sending commands to GDScript via WinUI3Host.call_handler().

using System;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
/// <summary>
/// Sends messages from the WinUI3 host application to the Godot engine.
/// This class wraps the <see cref="GodotWinUI3Embed.CallEngine"/> functionality
/// and provides a structured command sending pattern similar to DotNetBridge.
/// </summary>
/// <remarks>
/// <para>
/// Messages are sent to GDScript handlers registered via <c>WinUI3Host.register_handler(method, callable)</c>.
/// The sender also triggers the <c>WinUI3Host.host_message_received</c> signal on the GDScript side.
/// </para>
/// <para>
/// Usage from GDScript:
/// <code>
///   func _ready():
///       WinUI3Host.register_handler("set_device_info", _on_set_device_info)
///       WinUI3Host.register_handler("update_configuration", _on_update_configuration)
///
///   func _on_set_device_info(info: Dictionary) -> Dictionary:
///       device_info = info
///       return {"ok": true}
/// </code>
/// </para>
/// </remarks>
public sealed class HostInteropSender : IDisposable
{
	private readonly SynchronizationContext? _synchronizationContext;
	private readonly JsonSerializerOptions _jsonOptions;
	private bool _isDisposed;

	/// <summary>
	/// Gets the singleton instance of the sender.
	/// </summary>
	public static HostInteropSender? Instance { get; private set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="HostInteropSender"/> class.
	/// </summary>
	public HostInteropSender()
	{
		_synchronizationContext = SynchronizationContext.Current;
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};
		_jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

		Instance = this;
	}

	#region Public Methods - Send Data Commands

	/// <summary>
	/// Sends a data command to the Godot engine asynchronously.
	/// </summary>
	/// <param name="subCmd">The sub-command name (e.g., "result_device_status").</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This method is equivalent to <c>SendStDataCommand</c> in DotNetBridge.
	/// The command is sent with mainCmd = "st_data".
	/// </remarks>
	public void SendDataCommandAsync(string subCmd, string data, CancellationToken cancellationToken = default)
	{
		SendDataCommand(subCmd, data);
	}

	/// <summary>
	/// Sends a data command to the Godot engine.
	/// </summary>
	/// <param name="subCmd">The sub-command name (e.g., "result_device_status").</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	/// <remarks>
	/// This method is equivalent to <c>SendStDataCommand</c> in DotNetBridge.
	/// The command is sent with mainCmd = "st_data".
	/// </remarks>
	public void SendDataCommand(string subCmd, string data)
	{
		try
		{
			var command = new InteropCommandFormat
			{
				MainCmd = "st_data",
				SubCmd = subCmd,
				Data = data
			};

			if (command.SubCmd != "result_device_status") // Avoid excessive logging
			{
				System.Diagnostics.Debug.WriteLine($">>>>>>>>>>>>>>[{command.SubCmd}] = {TruncateString(command.Data, 200)}...<<<<<<<<<<<<");
			}

			SendCommand(command);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[FAIL: {ex.Message}] SendDataCommand: st_data / {subCmd}");
		}
	}

	/// <summary>
	/// Posts a data command to the Godot engine asynchronously (fire-and-forget).
	/// </summary>
	/// <param name="subCmd">The sub-command name.</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	public void PostDataCommand(string subCmd, string data)
	{
		SendDataCommand(subCmd, data);
	}

	#endregion

	#region Public Methods - Send UI Commands

	/// <summary>
	/// Sends a UI control command to the Godot engine asynchronously.
	/// </summary>
	/// <param name="subCmd">The sub-command name (e.g., "result_user_settings").</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This method is equivalent to <c>SendUIControlDataCommand</c> in DotNetBridge.
	/// The command is sent with mainCmd = "ui".
	/// </remarks>
	public void SendUICommandAsync(string subCmd, string data, CancellationToken cancellationToken = default)
	{
		SendUICommand(subCmd, data);
	}

	/// <summary>
	/// Sends a UI control command to the Godot engine.
	/// </summary>
	/// <param name="subCmd">The sub-command name (e.g., "result_user_settings").</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	/// <remarks>
	/// This method is equivalent to <c>SendUIControlDataCommand</c> in DotNetBridge.
	/// The command is sent with mainCmd = "ui".
	/// </remarks>
	public void SendUICommand(string subCmd, string data)
	{
		try
		{
			var command = new InteropCommandFormat
			{
				MainCmd = "ui",
				SubCmd = subCmd,
				Data = data
			};

			System.Diagnostics.Debug.WriteLine($"Send UI Control Data Command: >>>>>>>>>>>>>>[{command.MainCmd}  {command.SubCmd}] = {TruncateString(command.Data, 200)} <<<<<<<<<<<<");

			SendCommand(command);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[FAIL: {ex.Message}] SendUICommand: ui / {subCmd}");
		}
	}

	/// <summary>
	/// Posts a UI control command to the Godot engine asynchronously (fire-and-forget).
	/// </summary>
	/// <param name="subCmd">The sub-command name.</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	public void PostUICommand(string subCmd, string data)
	{
		_ = Task.Run(() => SendUICommand(subCmd, data));
	}

	#endregion

	#region Public Methods - Send Dialog Commands

	/// <summary>
	/// Sends a dialog control command to the Godot engine asynchronously.
	/// </summary>
	/// <param name="subCmd">The sub-command name (e.g., dialog response).</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	public Task SendDialogCommandAsync(string subCmd, string data, CancellationToken cancellationToken = default)
	{
		return Task.Run(() => SendDialogCommand(subCmd, data), cancellationToken);
	}

	/// <summary>
	/// Sends a dialog control command to the Godot engine.
	/// </summary>
	/// <param name="subCmd">The sub-command name (e.g., dialog response).</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	public void SendDialogCommand(string subCmd, string data)
	{
		try
		{
			var command = new InteropCommandFormat
			{
				MainCmd = "ui",
				SubCmd = subCmd,
				Data = data
			};

			System.Diagnostics.Debug.WriteLine($"Send Dialog Control Command: >>>>>>>>>>>>>>[{command.MainCmd}  {command.SubCmd}] = {TruncateString(command.Data, 200)} <<<<<<<<<<<<");

			SendCommand(command);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[FAIL: {ex.Message}] SendDialogCommand: ui / {subCmd}");
		}
	}

	/// <summary>
	/// Posts a dialog control command to the Godot engine asynchronously (fire-and-forget).
	/// </summary>
	/// <param name="subCmd">The sub-command name.</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	public void PostDialogCommand(string subCmd, string data)
	{
		_ = Task.Run(() => SendDialogCommand(subCmd, data));
	}

	#endregion

	#region Public Methods - Call Engine

	/// <summary>
	/// Calls a registered GDScript handler and returns the result.
	/// </summary>
	/// <param name="method">The method name to call.</param>
	/// <param name="argsJson">JSON-encoded array of arguments (optional).</param>
	/// <returns>JSON-encoded return value, or <c>null</c> if no handler returned a value.</returns>
	/// <exception cref="ArgumentException">Thrown when method is null or empty.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the engine bridge is not initialized.</exception>
	/// <remarks>
	/// This method directly wraps <see cref="GodotWinUI3Embed.CallEngine"/> for direct handler invocation.
	/// </remarks>
	public string? CallEngine(string method, string? argsJson = null)
	{
		if (string.IsNullOrEmpty(method))
		{
			throw new ArgumentException("method must be non-empty.", nameof(method));
		}

		try
		{
			var result = GodotWinUI3Embed.CallEngine(method, argsJson);
			System.Diagnostics.Debug.WriteLine($"[HostInteropSender] CallEngine({method}) -> {TruncateString(result, 200)}");
			return result;
		}
		catch (InvalidOperationException ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropSender] CallEngine failed: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Calls a registered GDScript handler asynchronously and returns the result.
	/// </summary>
	/// <param name="method">The method name to call.</param>
	/// <param name="argsJson">JSON-encoded array of arguments (optional).</param>
	/// <param name="cancellationToken">Optional cancellation token.</param>
	/// <returns>A task containing the JSON-encoded return value.</returns>
	public Task<string?> CallEngineAsync(string method, string? argsJson = null, CancellationToken cancellationToken = default)
	{
		return Task.Run(() => CallEngine(method, argsJson), cancellationToken);
	}

	/// <summary>
	/// Calls a registered GDScript handler with typed arguments.
	/// </summary>
	/// <typeparam name="T">The type of the result.</typeparam>
	/// <param name="method">The method name to call.</param>
	/// <param name="argsJson">JSON-encoded array of arguments (optional).</param>
	/// <returns>The deserialized result, or default value if no result.</returns>
	public T? CallEngine<T>(string method, string? argsJson = null)
	{
		var result = CallEngine(method, argsJson);
		if (result == null)
		{
			return default;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(result, _jsonOptions);
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropSender] Deserialization error: {ex.Message}");
			return default;
		}
	}

	/// <summary>
	/// Calls a registered GDScript handler with an object that will be serialized to JSON.
	/// </summary>
	/// <param name="method">The method name to call.</param>
	/// <param name="args">The arguments object to serialize.</param>
	/// <returns>JSON-encoded return value, or <c>null</c> if no handler returned a value.</returns>
	public string? CallEngineWithObject(string method, object? args = null)
	{
		var argsJson = args != null ? JsonSerializer.Serialize(args, _jsonOptions) : null;
		return CallEngine(method, argsJson);
	}

	/// <summary>
	/// Calls a registered GDScript handler with an object and returns a typed result.
	/// </summary>
	/// <typeparam name="T">The type of the result.</typeparam>
	/// <param name="method">The method name to call.</param>
	/// <param name="args">The arguments object to serialize.</param>
	/// <returns>The deserialized result, or default value if no result.</returns>
	public T? CallEngineWithObject<T>(string method, object? args = null)
	{
		var result = CallEngineWithObject(method, args);
		if (result == null)
		{
			return default;
		}

		try
		{
			return JsonSerializer.Deserialize<T>(result, _jsonOptions);
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropSender] Deserialization error: {ex.Message}");
			return default;
		}
	}

	#endregion

	#region Public Methods - Send Configuration

	/// <summary>
	/// Sends configuration data to the Godot engine.
	/// </summary>
	/// <param name="configuration">The configuration object to send.</param>
	/// <remarks>
	/// This method is equivalent to <c>SendConfiguration</c> in DotNetBridge.
	/// It calls the engine with method "on_configuration_changed".
	/// </remarks>
	public void SendConfiguration(object configuration)
	{
		var configJson = JsonSerializer.Serialize(configuration, _jsonOptions);
		CallEngine("on_configuration_changed", configJson);
	}

	/// <summary>
	/// Posts configuration data to the Godot engine asynchronously (fire-and-forget).
	/// </summary>
	/// <param name="configuration">The configuration object to send.</param>
	public void PostConfiguration(object configuration)
	{
		_ = Task.Run(() => SendConfiguration(configuration));
	}

	#endregion

	#region Public Methods - Send Raw

	/// <summary>
	/// Sends a raw command to the Godot engine with custom main command.
	/// </summary>
	/// <param name="mainCmd">The main command category.</param>
	/// <param name="subCmd">The sub-command name.</param>
	/// <param name="data">The JSON-encoded data to send.</param>
	public void SendRawCommand(string mainCmd, string subCmd, string data)
	{
		try
		{
			var command = new InteropCommandFormat
			{
				MainCmd = mainCmd,
				SubCmd = subCmd,
				Data = data
			};

			System.Diagnostics.Debug.WriteLine($"Send Raw Command: >>>>>>>>>>>>>>[{command.MainCmd}  {command.SubCmd}] = {TruncateString(command.Data, 200)} <<<<<<<<<<<<");

			SendCommand(command);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[FAIL: {ex.Message}] SendRawCommand: {mainCmd} / {subCmd}");
		}
	}

	#endregion

	#region Private Methods

	/// <summary>
	/// Sends a command to the Godot engine.
	/// </summary>
	private void SendCommand(InteropCommandFormat command)
	{
		try
		{
			// Combine mainCmd and subCmd into method name
			var method = $"response";
			var argsJson = command.ToString();

			void Invoke()
			{
				try
				{
					GodotWinUI3Embed.CallEngine(method, argsJson);
				}
				catch (Exception ex)
				{
					Debug.Write($"[HostInteropSender] SendCommand failed: {ex.Message}");
				}
			}

			if (_synchronizationContext == null || SynchronizationContext.Current == _synchronizationContext)
			{
				Invoke();
			}
			else
			{
				_synchronizationContext.Post(_ => Invoke(), null);
			}
		}
		catch (InvalidOperationException ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropSender] Engine bridge not initialized: {ex.Message}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[HostInteropSender] SendCommand failed: {ex.Message}");
		}
	}

	/// <summary>
	/// Truncates a string to a maximum length for logging purposes.
	/// </summary>
	private static string TruncateString(string? value, int maxLength)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
	}

	#endregion

	#region IDisposable

	/// <summary>
	/// Releases all resources used by the <see cref="HostInteropSender"/>.
	/// </summary>
	public void Dispose()
	{
		if (_isDisposed)
		{
			return;
		}

		Instance = null;
		_isDisposed = true;
		GC.SuppressFinalize(this);
	}

	#endregion

	#region Nested Types

	/// <summary>
	/// Represents an interop command format for internal processing.
	/// </summary>
	private sealed class InteropCommandFormat
	{
		public string? MainCmd { get; init; }
		public string? SubCmd { get; init; }
		public string? Data { get; init; }

		public override string ToString()
		{
			String[] data = [MainCmd ?? "", SubCmd ?? "", Data ?? ""];
			var responseString = JsonSerializer.Serialize(data);

			return responseString;
		}
	}

	#endregion
}
