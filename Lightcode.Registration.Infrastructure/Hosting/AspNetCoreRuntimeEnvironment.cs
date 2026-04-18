using Lightcode.Registration.Application.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Lightcode.Registration.Infrastructure.Hosting;

public sealed class AspNetCoreRuntimeEnvironment(IHostEnvironment hostEnvironment) : IRuntimeEnvironment
{
    public bool IsDevelopment =>
        string.Equals(hostEnvironment.EnvironmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);
}
