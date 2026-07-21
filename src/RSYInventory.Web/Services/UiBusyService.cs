namespace RSYInventory.Web.Services;

/// <summary>
/// Circuit-scoped busy counter for blocking the UI with a spinner during async work.
/// </summary>
public sealed class UiBusyService
{
    private int _count;

    public bool IsBusy => _count > 0;
    public string Message { get; private set; } = "Trabajando…";
    public event Action? Changed;

    public IDisposable Begin(string? message = null)
    {
        _count++;
        if (!string.IsNullOrWhiteSpace(message))
            Message = message;
        else if (_count == 1)
            Message = "Trabajando…";

        Changed?.Invoke();
        return new Releaser(this);
    }

    public async Task RunAsync(Func<Task> action, string? message = null)
    {
        using (Begin(message))
            await action();
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> action, string? message = null)
    {
        using (Begin(message))
            return await action();
    }

    private void End()
    {
        _count = Math.Max(0, _count - 1);
        if (_count == 0)
            Message = "Trabajando…";
        Changed?.Invoke();
    }

    private sealed class Releaser(UiBusyService owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            owner.End();
        }
    }
}
