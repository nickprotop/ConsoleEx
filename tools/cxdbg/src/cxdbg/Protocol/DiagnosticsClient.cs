// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Cxdbg.Protocol;

/// <summary>
/// One conversation with a running application.
/// </summary>
/// <remarks>
/// The protocol is line-delimited JSON: one object per line each way. The
/// server greets first with its protocol version and process id, then answers
/// each request in order.
/// <para>
/// One client at a time, by the server's design - concurrent probes mutating
/// the same application would make every answer ambiguous.
/// </para>
/// </remarks>
public sealed class DiagnosticsClient : IDisposable
{
	private readonly Socket _socket;
	private readonly NetworkStream _stream;
	private readonly StreamReader _reader;
	private readonly StreamWriter _writer;
	private bool _disposed;

	/// <summary>The application's process id, from its greeting.</summary>
	public int Pid { get; }

	/// <summary>The protocol version the application speaks.</summary>
	public int Protocol { get; }

	/// <summary>Where this client is connected.</summary>
	public string SocketPath { get; }

	private DiagnosticsClient(string path, Socket socket, JsonNode hello)
	{
		SocketPath = path;
		_socket = socket;
		_stream = new NetworkStream(socket, ownsSocket: false);
		_reader = new StreamReader(_stream, Encoding.UTF8);
		_writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };

		Pid = hello["pid"]?.GetValue<int>() ?? 0;
		Protocol = hello["protocol"]?.GetValue<int>() ?? 0;
	}

	/// <summary>Connects to an application's diagnostics socket.</summary>
	/// <param name="path">The socket path.</param>
	/// <param name="greetingTimeout">
	/// How long to wait for the greeting. A server already serving another
	/// client accepts the connection and then says nothing, so the timeout is
	/// what distinguishes "busy" from "slow".
	/// </param>
	/// <exception cref="CxdbgException">
	/// Nothing is listening, the application is busy, or it is not speaking this
	/// protocol.
	/// </exception>
	public static DiagnosticsClient Connect(string path, TimeSpan? greetingTimeout = null)
	{
		var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

		try
		{
			socket.Connect(new UnixDomainSocketEndPoint(path));
		}
		catch (SocketException ex)
		{
			socket.Dispose();

			// "Connection refused" is true and useless. A stale socket file from a
			// crashed app looks exactly like a path typed wrong, and in both cases
			// what helps is the path and the fact that nothing is there.
			throw new CxdbgException(
				$"nothing is listening at {path}. The application may have exited, "
				+ "or it was started without a diagnostics socket.", inner: ex);
		}

		socket.ReceiveTimeout = (int)(greetingTimeout ?? TimeSpan.FromSeconds(10)).TotalMilliseconds;

		try
		{
			using var peek = new StreamReader(
				new NetworkStream(socket, ownsSocket: false), Encoding.UTF8, leaveOpen: true);

			string? line = peek.ReadLine();
			if (line == null)
				throw Busy(path);

			var hello = JsonNode.Parse(line)
				?? throw new CxdbgException($"{path} did not greet with JSON");

			socket.ReceiveTimeout = 0;
			return new DiagnosticsClient(path, socket, hello);
		}
		catch (IOException)
		{
			socket.Dispose();
			throw Busy(path);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	private static CxdbgException Busy(string path) => new(
		$"the application at {path} is busy with another client. "
		+ "It serves one at a time, so that concurrent probes cannot make its answers ambiguous.");

	/// <summary>Sends one verb and returns its reply.</summary>
	/// <param name="verb">The command name, as the protocol spells it.</param>
	/// <param name="args">Extra fields to send alongside it.</param>
	/// <returns>The reply object.</returns>
	/// <exception cref="CxdbgException">The application answered with a failure.</exception>
	public JsonNode Send(string verb, IReadOnlyDictionary<string, JsonNode?>? args = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var request = new JsonObject { ["cmd"] = verb };
		if (args != null)
			foreach (var (k, v) in args) request[k] = v;

		_writer.WriteLine(request.ToJsonString());

		// ReadLine, not a fixed buffer: `image` answers with a base64 PNG of tens
		// of kilobytes on a single line, and a short read looks like corruption.
		string? line = _reader.ReadLine()
			?? throw new CxdbgException($"the application at {SocketPath} closed the connection");

		var reply = JsonNode.Parse(line)
			?? throw new CxdbgException($"the application at {SocketPath} sent a reply that is not JSON");

		if (reply["ok"]?.GetValue<bool>() != true)
		{
			string message = reply["error"]?.GetValue<string>()
				?? $"'{verb}' failed, without saying why";

			// An unknown verb comes back with the list the application knows.
			// That list IS the answer to what the user was reaching for, so it
			// travels with the error rather than being dropped.
			if (reply["known"] is JsonArray known && known.Count > 0)
			{
				message += "\n\nThis application knows: "
					+ string.Join(", ", known.Select(k => k?.GetValue<string>()));
			}

			throw new CxdbgException(message, reply["code"]?.GetValue<string>());
		}

		return reply;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		try { _writer.Dispose(); } catch { /* closing */ }
		try { _reader.Dispose(); } catch { /* closing */ }
		try { _stream.Dispose(); } catch { /* closing */ }
		_socket.Dispose();
	}
}
