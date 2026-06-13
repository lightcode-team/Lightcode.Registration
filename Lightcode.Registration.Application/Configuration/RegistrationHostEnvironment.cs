namespace Lightcode.Registration.Application.Configuration;

/// <summary>
/// Carrega variáveis de um ficheiro <c>.env</c> sem substituir variáveis já definidas no ambiente.
/// Procura no diretório atual e sobe até encontrar um marcador da raiz do projeto.
/// </summary>
public static class RegistrationHostEnvironment
{
    public static void LoadDotEnvIfPresent(string? envFilePath = null)
    {
        if (IsDevelopment())
            return;

        var path = ResolveEnvFilePath(envFilePath);
        if (path is null || !File.Exists(path))
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                continue;

            var value = Unquote(line[(separator + 1)..].Trim());
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? ResolveEnvFilePath(string? envFilePath)
    {
        if (!string.IsNullOrWhiteSpace(envFilePath))
            return envFilePath;

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;

            if (LooksLikeRepoRoot(current))
                return candidate;

            current = current.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), ".env");
    }

    private static bool IsDevelopment()
    {
        var aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var dotnetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.Equals(aspNetCoreEnvironment, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dotnetEnvironment, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRepoRoot(DirectoryInfo directory) =>
        File.Exists(Path.Combine(directory.FullName, "docker-compose.yml"))
        && File.Exists(Path.Combine(directory.FullName, "Lightcode.Registration.slnx"));

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }
}
