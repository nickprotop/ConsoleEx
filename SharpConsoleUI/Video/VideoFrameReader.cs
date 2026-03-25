using System.Diagnostics;
using System.Globalization;
using SharpConsoleUI.Configuration;

namespace SharpConsoleUI.Video
{
    /// <summary>
    /// Reads video frames as raw RGB24 byte arrays by piping from an FFmpeg subprocess.
    /// Each frame is width * height * 3 bytes (R, G, B per pixel, row-major).
    /// </summary>
    public sealed class VideoFrameReader : IDisposable
    {
        private Process? _ffmpeg;
        private Stream? _stdout;
        private bool _disposed;

        /// <summary>Video width in pixels.</summary>
        public int Width { get; }

        /// <summary>Video height in pixels.</summary>
        public int Height { get; }

        /// <summary>Video frame rate.</summary>
        public double Fps { get; }

        /// <summary>Video duration in seconds, or 0 if unknown.</summary>
        public double DurationSeconds { get; }

        /// <summary>Bytes per frame (Width * Height * 3).</summary>
        public int FrameSize { get; }

        private VideoFrameReader(Process ffmpeg, int width, int height, double fps, double duration)
        {
            _ffmpeg = ffmpeg;
            _stdout = ffmpeg.StandardOutput.BaseStream;
            Width = width;
            Height = height;
            Fps = fps;
            DurationSeconds = duration;
            FrameSize = width * height * 3;
        }

        /// <summary>
        /// Checks if the FFmpeg executable is available on the system PATH.
        /// </summary>
        public static bool IsFfmpegAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(VideoDefaults.FfmpegStartTimeoutMs);
                return p?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Opens a video file and starts the FFmpeg decode process.
        /// Probes metadata first, then launches the raw frame pipe.
        /// </summary>
        /// <param name="filePath">Path to the video file.</param>
        /// <param name="targetWidth">Target output width in pixels. FFmpeg scales to this.</param>
        /// <param name="targetHeight">Target output height in pixels. FFmpeg scales to this.</param>
        /// <param name="startTime">Optional start time in seconds for seeking.</param>
        /// <returns>A VideoFrameReader ready to read frames.</returns>
        /// <exception cref="InvalidOperationException">If FFmpeg is not found or fails to start.</exception>
        public static VideoFrameReader Open(string filePath, int targetWidth, int targetHeight, double startTime = 0)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Video file not found.", filePath);

            // Probe metadata
            var (fps, duration) = ProbeMetadata(filePath);

            // Launch FFmpeg to decode and scale to target dimensions, output raw RGB24
            string seekArg = startTime > 0 ? $"-ss {startTime:F3} " : "";
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"{seekArg}-i \"{filePath}\" -f rawvideo -pix_fmt rgb24 " +
                            $"-s {targetWidth}x{targetHeight} -an -sn -v quiet -",
                RedirectStandardInput = true,  // Prevent FFmpeg from inheriting terminal stdin
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start FFmpeg process.");

            // Discard stderr to prevent pipe deadlock
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();

            return new VideoFrameReader(process, targetWidth, targetHeight, fps, duration);
        }

        /// <summary>
        /// Reads the next frame into the provided buffer.
        /// </summary>
        /// <param name="buffer">Must be at least FrameSize bytes.</param>
        /// <returns>True if a full frame was read; false if end-of-stream.</returns>
        public bool ReadFrame(byte[] buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_stdout == null)
                return false;

            int totalRead = 0;
            while (totalRead < FrameSize)
            {
                int bytesRead = _stdout.Read(buffer, totalRead, FrameSize - totalRead);
                if (bytesRead == 0)
                    return false; // EOF
                totalRead += bytesRead;
            }
            return true;
        }

        /// <summary>
        /// Reads the next frame into the provided buffer asynchronously.
        /// </summary>
        /// <param name="buffer">Must be at least FrameSize bytes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if a full frame was read; false if end-of-stream.</returns>
        public async Task<bool> ReadFrameAsync(byte[] buffer, CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_stdout == null)
                return false;

            int totalRead = 0;
            while (totalRead < FrameSize)
            {
                int bytesRead = await _stdout.ReadAsync(
                    buffer.AsMemory(totalRead, FrameSize - totalRead), ct);
                if (bytesRead == 0)
                    return false;
                totalRead += bytesRead;
            }
            return true;
        }

        /// <summary>
        /// Probes video metadata using ffprobe.
        /// </summary>
        private static (double Fps, double Duration) ProbeMetadata(string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            double fps = 24;
            double duration = 0;

            try
            {
                using var probe = Process.Start(psi);
                if (probe == null) return (fps, duration);

                probe.WaitForExit(VideoDefaults.FfmpegStartTimeoutMs);
                string json = probe.StandardOutput.ReadToEnd();

                // Simple JSON parsing — extract fps and duration without a JSON library
                fps = ExtractFps(json);
                duration = ExtractDuration(json);
            }
            catch
            {
                // ffprobe not available or failed — use defaults
            }

            return (Math.Clamp(fps, VideoDefaults.MinFps, VideoDefaults.MaxFps), duration);
        }

        /// <summary>Extracts FPS from ffprobe JSON output.</summary>
        private static double ExtractFps(string json)
        {
            // Look for "r_frame_rate": "30/1" or "avg_frame_rate": "29.97/1"
            foreach (var key in new[] { "avg_frame_rate", "r_frame_rate" })
            {
                int idx = json.IndexOf($"\"{key}\"", StringComparison.Ordinal);
                if (idx < 0) continue;

                int colonIdx = json.IndexOf(':', idx);
                if (colonIdx < 0) continue;

                int quoteStart = json.IndexOf('"', colonIdx);
                if (quoteStart < 0) continue;
                quoteStart++;

                int quoteEnd = json.IndexOf('"', quoteStart);
                if (quoteEnd < 0) continue;

                string val = json[quoteStart..quoteEnd];
                if (val.Contains('/'))
                {
                    var parts = val.Split('/');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out double num) &&
                        double.TryParse(parts[1], out double den) &&
                        den > 0)
                    {
                        double result = num / den;
                        if (result > 0) return result;
                    }
                }
                else if (double.TryParse(val, out double direct) && direct > 0)
                {
                    return direct;
                }
            }
            return 24;
        }

        /// <summary>Extracts duration from ffprobe JSON output.</summary>
        private static double ExtractDuration(string json)
        {
            int idx = json.IndexOf("\"duration\"", StringComparison.Ordinal);
            if (idx < 0) return 0;

            int colonIdx = json.IndexOf(':', idx);
            if (colonIdx < 0) return 0;

            int quoteStart = json.IndexOf('"', colonIdx);
            if (quoteStart < 0) return 0;
            quoteStart++;

            int quoteEnd = json.IndexOf('"', quoteStart);
            if (quoteEnd < 0) return 0;

            return double.TryParse(json[quoteStart..quoteEnd],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double d) ? d : 0;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _stdout = null;

            if (_ffmpeg != null && !_ffmpeg.HasExited)
            {
                try { _ffmpeg.Kill(entireProcessTree: true); } catch { }
            }
            _ffmpeg?.Dispose();
            _ffmpeg = null;
        }
    }
}
