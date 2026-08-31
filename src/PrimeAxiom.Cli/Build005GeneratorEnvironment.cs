using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PrimeAxiom.Cli;

internal sealed record Build005GeneratorEnvironmentReceipt(
    string Schema,
    string ProtocolId,
    string RuntimeVersion,
    string FrameworkDescription,
    string OsDescription,
    string OsArchitecture,
    string ProcessArchitecture,
    string Platform);

internal static class Build005GeneratorEnvironment
{
    public static Build005GeneratorEnvironmentReceipt Capture() => new(
        "prime-axiom-build005-generator-environment-v1",
        Build005Protocol.ProtocolId,
        Environment.Version.ToString(),
        RuntimeInformation.FrameworkDescription,
        RuntimeInformation.OSDescription,
        RuntimeInformation.OSArchitecture.ToString(),
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.OSVersion.Platform.ToString());

    public static void WriteNew(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(fullPath) ??
            throw new InvalidOperationException(
                "The Build 005 generator-environment receipt must have a parent directory.");
        Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(Capture(), Build005Protocol.JsonOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n') + "\n";
        var bytes = new UTF8Encoding(false).GetBytes(json);
        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                "Refusing to overwrite the Build 005 generator-environment receipt.",
                exception);
        }
    }
}
