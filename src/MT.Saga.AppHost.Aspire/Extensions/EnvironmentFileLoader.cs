namespace MT.Saga.AppHost.Aspire.Extensions;

public static class EnvironmentFileLoader
{
    private const string EnvRootOverrideVariable = "ASPIRE_ENV_ROOT";

    public static void Load(string? rootDirectory = null)
    {
        var resolvedRoot = ResolveRootDirectory(rootDirectory);
        var envFile = Path.Combine(resolvedRoot, ".env");

        if (!File.Exists(envFile))
        {
            return;
        }

        DotNetEnv.Env.Load(envFile);
    }

    private static string ResolveRootDirectory(string? rootDirectory)
    {
        if (!string.IsNullOrWhiteSpace(rootDirectory))
        {
            return Path.GetFullPath(rootDirectory);
        }

        var environmentRoot = Environment.GetEnvironmentVariable(EnvRootOverrideVariable);
        if (!string.IsNullOrWhiteSpace(environmentRoot))
        {
            var normalizedEnvironmentRoot = Path.GetFullPath(environmentRoot);
            if (Directory.Exists(normalizedEnvironmentRoot))
            {
                return normalizedEnvironmentRoot;
            }
        }

        var currentDirectoryRoot = FindDirectoryContainingFile(Directory.GetCurrentDirectory(), ".env");
        if (currentDirectoryRoot is not null)
        {
            return currentDirectoryRoot;
        }

        var appBaseDirectoryRoot = FindDirectoryContainingFile(AppContext.BaseDirectory, ".env");
        if (appBaseDirectoryRoot is not null)
        {
            return appBaseDirectoryRoot;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? FindDirectoryContainingFile(string startPath, string fileName)
    {
        var directory = new DirectoryInfo(startPath);

        while (directory is not null)
        {
            var filePath = Path.Combine(directory.FullName, fileName);
            if (File.Exists(filePath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
