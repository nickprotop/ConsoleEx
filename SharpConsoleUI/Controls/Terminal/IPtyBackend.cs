namespace SharpConsoleUI.Controls.Terminal;

/// <summary>
/// Abstracts the platform-specific PTY (pseudo-terminal) backend.
/// Implementations: <see cref="LinuxPtyBackend"/> (openpty), <see cref="WindowsPtyBackend"/> (ConPTY).
/// </summary>
internal interface IPtyBackend : IDisposable
{
    /// <summary>Resize the terminal to the given dimensions.</summary>
    void Resize(int rows, int cols);

    /// <summary>
    /// Read output bytes from the PTY.
    /// Returns the number of bytes read, or 0 on EOF / backend closed.
    /// </summary>
    int Read(byte[] buf, int count);

    /// <summary>Write keyboard/mouse bytes to the PTY.</summary>
    void Write(byte[] buf, int count);
}
