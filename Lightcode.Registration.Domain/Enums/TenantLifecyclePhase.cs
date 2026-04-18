namespace Lightcode.Registration.Domain.Enums;

/// <summary>Fases do ciclo de vida do tenant (provisionamento, operação, suspensão).</summary>
public enum TenantLifecyclePhase
{
    Created = 0,
    Ready = 1,
    Suspended = 2
}
