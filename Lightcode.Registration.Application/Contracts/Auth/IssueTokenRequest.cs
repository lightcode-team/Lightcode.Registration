namespace Lightcode.Registration.Application.Contracts.Auth;

/// <summary>Pedido de token; o tenant deve ser enviado no cabeçalho HTTP (ex.: <c>X-Tenant-Id</c>).</summary>
public sealed record IssueTokenRequest(string Username, string Password);
