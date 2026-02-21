namespace DotNetIDE;

public static class FileService
{
    public static string ReadFile(string path)
    {
        if (IsBinaryFile(path))
            throw new InvalidOperationException("Binary file cannot be opened in text editor.");
        return File.ReadAllText(path, System.Text.Encoding.UTF8);
    }

    public static void WriteFile(string path, string content)
    {
        File.WriteAllText(path, content, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static bool IsBinaryFile(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buffer = new byte[Math.Min(8192, fs.Length)];
            int read = fs.Read(buffer, 0, buffer.Length);
            // Check for null bytes — binary indicator
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == 0) return true;
            }
            return false;
        }
        catch { return true; }
    }

    public static bool Exists(string path) => File.Exists(path);

    public static string GetExtension(string path) => Path.GetExtension(path).ToLowerInvariant();
}
