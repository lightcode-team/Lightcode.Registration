namespace Lightcode.Registration.AspNetCore.Hosting;

/// <summary>
/// Carrega variáveis de um ficheiro <c>.env</c> na raiz do projeto (desenvolvimento local).
/// Não substitui variáveis já definidas no ambiente (Docker, CI, launchSettings).
/// </summary>
public static class RegistrationHostEnvironment
{
    public static void LoadDotEnvIfPresent(string? envFilePath = null)
    {
        var path = envFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path))
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
