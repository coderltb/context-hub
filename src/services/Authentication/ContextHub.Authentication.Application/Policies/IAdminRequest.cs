namespace ContextHub.Authentication.Application.Policies;

/// <summary>
/// Marks administrative use cases that must pass the shared authorization pipeline.
/// </summary>
public interface IAdminRequest : IRequiresAuthorization;
