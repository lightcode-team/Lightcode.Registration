namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Abstrai ambiente de execução (ex.: Development) sem referência a ASP.NET na camada de aplicação.</summary>
public interface IRuntimeEnvironment
{
    bool IsDevelopment { get; }
}
