namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Persistência de conta de utilizador no database do tenant (coleção Users).</summary>
public interface IUserAccountWriter
{
    Task<bool> EmailExistsAsync(string tenantId, string email, CancellationToken cancellationToken = default);

    Task<bool> UsernameExistsAsync(string tenantId, string username, CancellationToken cancellationToken = default);

    /// <returns>Identificador persistido (<c>_id</c> MongoDB).</returns>
    Task<string> InsertAsync(string tenantId, string documentJson, CancellationToken cancellationToken = default);

    /// <returns>JSON do documento ou <c>null</c> se não existir.</returns>
    Task<string?> GetUserDocumentJsonAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    Task ReplaceUserDocumentAsync(string tenantId, string userId, string documentJson, CancellationToken cancellationToken = default);

    Task<bool> EmailTakenByOtherUserAsync(
        string tenantId,
        string email,
        string excludeUserId,
        CancellationToken cancellationToken = default);

    Task<bool> UsernameTakenByOtherUserAsync(
        string tenantId,
        string username,
        string excludeUserId,
        CancellationToken cancellationToken = default);

    /// <param name="reminderKind">30 ou 15 (dias antes da expiração).</param>
    Task MarkExpiryReminderSentAsync(
        string tenantId,
        string userId,
        int reminderKind,
        DateTime sentUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Define <c>status</c> como Expired se a conta estiver Active e a data de expiração já tiver passado.</summary>
    Task<bool> TryMarkRegistrationExpiredAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    /// <summary>Confirma email por código ou token; ativa a conta se válido.</summary>
    Task<bool> TryConfirmEmailAsync(
        string tenantId,
        string email,
        string secretPlain,
        CancellationToken cancellationToken = default);

    /// <returns><c>status</c> do documento ou <c>null</c> se não existir.</returns>
    Task<string?> GetUserStatusAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListUserDocumentsJsonAsync(string tenantId, CancellationToken cancellationToken = default);
}
