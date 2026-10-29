namespace ContextHub.Core.Interfaces.Messaging.Commands;

/// <summary>
/// Represents a command that completes without returning a domain value.
/// </summary>
public interface ICommand : IRequest<Unit>;

/// <summary>
/// Represents a command that returns a domain value.
/// </summary>
/// <typeparam name="TResponse">The value returned by the command handler.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>;
