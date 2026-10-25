// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Net.Sockets;
using System.Text.Json.Nodes;

namespace WebHost;

/// <summary>
/// Talks to a running SharpConsoleUI application over its diagnostics socket.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately the only way this host reaches the application. It holds no reference to
/// the window system, the driver or any control - everything it knows about the screen
/// arrives as JSON over a unix socket. That is the point of the example: the same code
/// would work against an application in another process, started by someone else.
/// </para>
/// <para>
/// One connection, reused. The protocol is a request and a reply per line, so a single
/// connection is enough and reconnecting per request would cost more than it saves.
/// Serialised with a semaphore because several browser requests arrive at once and the
/// protocol has no way to tell two interleaved replies apart - which is exactly what the
/// request <c>id</c> would solve if this pipelined.
/// </para>
/// </remarks>
public sealed class DiagnosticsClient : IDisposable
{
	private readonly string _socketPath;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private Socket? _socket;
	private StreamReader? _reader;
	private StreamWriter? _writer;
	private long _nextId;

	public DiagnosticsClient(string socketPath) => _socketPath = socketPath;

	/// <summary>Sends one request and returns its reply.</summary>
	/// <param name="request">The verb and its arguments.</param>
	/// <param name="ct">Cancels the wait.</param>
	/// <returns>The reply, or an error object if the socket could not be reached.</returns>
	public async Task<JsonObject> AskAsync(JsonObject request, CancellationToken ct = default)
	{
		await _gate.WaitAsync(ct);
		try
		{
			await EnsureConnectedAsync(ct);

			// An id is not needed while this is strictly one reply per request, but it
			// costs nothing and makes a mismatch visible rather than silent if that ever
			// changes.
			string id = (++_nextId).ToString();
			request["id"] = id;

			await _writer!.WriteLineAsync(request.ToJsonString().AsMemory(), ct);
			string? line = await _reader!.ReadLineAsync(ct);

			if (line == null)
			{
				Reset();
				return Failure("the application closed the connection");
			}

			return JsonNode.Parse(line)?.AsObject() ?? Failure("reply was not a JSON object");
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// A browser refresh should not take the host down with it.
			Reset();
			return Failure($"{ex.GetType().Name}: {ex.Message}");
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task EnsureConnectedAsync(CancellationToken ct)
	{
		if (_socket is { Connected: true }) return;

		Reset();

		var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		await socket.ConnectAsync(new UnixDomainSocketEndPoint(_socketPath), ct);

		var stream = new NetworkStream(socket, ownsSocket: false);
		_socket = socket;
		_reader = new StreamReader(stream);
		_writer = new StreamWriter(stream) { AutoFlush = true };

		// The hello banner is unsolicited and carries no id; read it so it cannot be
		// mistaken for the first reply.
		await _reader.ReadLineAsync(ct);
	}

	private void Reset()
	{
		try { _reader?.Dispose(); } catch { }
		try { _writer?.Dispose(); } catch { }
		try { _socket?.Dispose(); } catch { }
		_reader = null;
		_writer = null;
		_socket = null;
	}

	private static JsonObject Failure(string message) =>
		new() { ["ok"] = false, ["error"] = message };

	public void Dispose()
	{
		Reset();
		_gate.Dispose();
	}
}
