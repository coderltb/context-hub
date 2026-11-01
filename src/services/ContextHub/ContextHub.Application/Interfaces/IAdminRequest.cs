namespace ContextHub.Application.Interfaces;

/// <summary>
/// Marks an administration use case that requires a current Owner grant before validation or business work.
/// </summary>
public interface IAdminRequest : IRequiresAuthorization;
