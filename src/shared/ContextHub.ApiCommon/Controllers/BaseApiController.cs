namespace ContextHub.ApiCommon.Controllers;

/// <summary>
/// Shares controller conventions and constructor-injected dispatch without introducing service location or business logic.
/// </summary>
/// <param name="mediator">The Application dispatcher supplied by the host container.</param>
[ApiController]
public abstract class BaseApiController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Gets the dispatcher used by derived HTTP adapters; handlers own their response envelope.
    /// </summary>
    protected IMediator Mediator { get; } = mediator;
}
