using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GrantMatcher.Client.Utilities;

/// <summary>
/// Helper for implementing lazy loading and intersection observer
/// Useful for loading images and components only when they're visible
/// </summary>
public class LazyLoadHelper : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private readonly Dictionary<string, DotNetObjectReference<LazyLoadCallback>> _callbacks = new();

    public LazyLoadHelper(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Initializes the lazy load module
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/lazyload.js");
        }
    }

    /// <summary>
    /// Observes an element and triggers callback when it becomes visible
    /// </summary>
    public async Task<string> ObserveAsync(ElementReference element, Func<Task> onVisible, double threshold = 0.1)
    {
        await InitializeAsync();

        var callbackId = Guid.NewGuid().ToString();
        var callback = new LazyLoadCallback(onVisible);
        var reference = DotNetObjectReference.Create(callback);
        _callbacks[callbackId] = reference;

        await _module!.InvokeVoidAsync("observeElement", element, reference, callbackId, threshold);

        return callbackId;
    }

    /// <summary>
    /// Stops observing an element
    /// </summary>
    public async Task UnobserveAsync(string callbackId)
    {
        if (_module != null && _callbacks.TryGetValue(callbackId, out var reference))
        {
            await _module.InvokeVoidAsync("unobserveElement", callbackId);
            _callbacks.Remove(callbackId);
            reference.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var reference in _callbacks.Values)
        {
            reference.Dispose();
        }
        _callbacks.Clear();

        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }

    public class LazyLoadCallback
    {
        private readonly Func<Task> _onVisible;

        public LazyLoadCallback(Func<Task> onVisible)
        {
            _onVisible = onVisible;
        }

        [JSInvokable]
        public async Task OnVisible()
        {
            await _onVisible();
        }
    }
}
