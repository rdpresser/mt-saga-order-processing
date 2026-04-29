namespace MT.Saga.AppHost.Aspire.Extensions;

public static class DotNetRuntimeBootstrapper
{
    public static void ConfigurePreferredDotNetHost()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            return;
        }

        var dotnetRoot = Path.Combine(homeDirectory, ".dotnet");
        var dotnetHost = Path.Combine(dotnetRoot, "dotnet");

        if (!File.Exists(dotnetHost))
        {
            return;
        }

        Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot);
        Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", dotnetHost);
        Environment.SetEnvironmentVariable("DOTNET_MULTILEVEL_LOOKUP", "0");

        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = currentPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!pathEntries.Contains(dotnetRoot, StringComparer.Ordinal))
        {
            var updatedPath = string.IsNullOrWhiteSpace(currentPath)
                ? dotnetRoot
                : string.Join(Path.PathSeparator, dotnetRoot, currentPath);
            Environment.SetEnvironmentVariable("PATH", updatedPath);
        }
    }
}