namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Persistência de conta de utilizador no database do tenant (coleção Users).</summary>
public interface IUserAccountWriter
{
    Task<bool> EmailExistsAsync(string tenantId, string email, CancellationToken cancellationToken = default);

    Task InsertAsync(string tenantId, string documentJson, CancellationToken cancellationToken = default);
}
