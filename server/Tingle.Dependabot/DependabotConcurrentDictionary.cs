using System.Collections.Concurrent;
using System.Reflection;

namespace Tingle.Dependabot;

/// <summary>
/// Represents a thread-safe collection of key/value pairs that can be accessed by
/// multiple threads in the Dependabot asynchronously.
/// </summary>
/// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
[DefaultMember("Item")]
public sealed class DependabotConcurrentDictionary<TKey, TValue> : ConcurrentDictionary<TKey, Task<TValue>> where TKey : notnull
{
    /// <inheritdoc/>
    [Obsolete("Use '" + nameof(GetOrAddAsync) + "(...)' instead")]
    public new Task<TValue> GetOrAdd(TKey key, Task<TValue> value) => base.GetOrAdd(key, value);

    /// <inheritdoc/>
    [Obsolete("Use '" + nameof(GetOrAddAsync) + "(...)' instead")]
    public new Task<TValue> GetOrAdd(TKey key, Func<TKey, Task<TValue>> valueFactory) => base.GetOrAdd(key, valueFactory);

    /// <inheritdoc/>
    [Obsolete("Use '" + nameof(GetOrAddAsync) + "(...)' instead")]
    public new Task<TValue> GetOrAdd<TArg>(TKey key, Func<TKey, TArg, Task<TValue>> valueFactory, TArg factoryArgument) => base.GetOrAdd(key, valueFactory, factoryArgument);

    // Code below is inspired by https://gist.github.com/davidfowl/3dac8f7b3d141ae87abf770d5781feed#file-concurrentdictionaryextensions-cs-L53-L87

    /// <summary>
    /// Asynchronously adds a key/value pair to the <see cref="ConcurrentDictionary{TKey, TValue}"/>
    /// by using the specified function if the key does not already exist.
    /// Returns the new value, or the existing value if the key exists.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="valueFactory">The function used to generate a value for the key.</param>
    /// <param name="cancellationToken">
    /// Optional <see cref="CancellationToken"/> to propagate notifications that the operation should be cancelled.
    /// </param>
    /// <exception cref="ArgumentNullException">key or valueFactory is null.</exception>
    /// <exception cref="OverflowException">The dictionary contains too many elements.</exception>
    /// <exception cref="OperationCanceledException">The token has had cancellation requested.</exception>
    /// <exception cref="ObjectDisposedException">The associated <see cref="CancellationTokenSource"/> has been disposed.</exception>
    /// <returns>
    /// The value for the key.
    /// This will be either the existing value for the key if the key is already in the dictionary,
    /// or the new value if the key was not in the dictionary.
    /// </returns>
    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> valueFactory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetValue(key, out var task)) return await task.ConfigureAwait(false);

            // This is the task that we'll return to all waiters. We'll complete it when the factory is complete
            var tcs = new TaskCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (TryAdd(key, tcs.Task)) // Adding this means the factory is only called once (only when addition works) which is better than GetOrAdd(...)
            {
                try
                {
                    var value = await valueFactory(key, cancellationToken).ConfigureAwait(false);
                    tcs.TrySetResult(value);
                    return await tcs.Task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Make sure all waiters see the exception
                    tcs.SetException(ex);

                    // We remove the entry if the factory failed so it's not a permanent failure
                    // and future gets can retry (this could be a resilience pipeline)
                    TryRemove(key, out _);
                    throw;
                }
            }
        }
    }
}
