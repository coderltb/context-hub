namespace ContextHub.Core.Messaging.Authorization;

/// <summary>
/// Marks a protected request that must have an explicit service-owned policy before its handler may run.
/// </summary>
public interface IRequiresAuthorization
{
}
