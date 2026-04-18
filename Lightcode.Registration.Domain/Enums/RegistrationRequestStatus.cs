namespace Lightcode.Registration.Domain.Enums;

/// <summary>Estados de um pedido de registro (uso futuro em fluxos de cadastro).</summary>
public enum RegistrationRequestStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4
}
