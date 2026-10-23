namespace ContextHub.Core.Messaging.Queries;

/// <summary>
/// Represents a read request that returns a value without changing application state.
/// </summary>
/// <typeparam name="TResponse">The value returned by the query handler.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>;
