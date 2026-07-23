namespace RSYInventory.Web.Services;

public enum ToastKind
{
    Success,
    Error,
    Info
}

public sealed class ToastMessage
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Text { get; init; } = string.Empty;
    public ToastKind Kind { get; init; }
    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
}

/// <summary>Circuit-scoped toast notifications (bottom-right).</summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _items = [];
    private readonly object _gate = new();

    public event Action? Changed;

    public IReadOnlyList<ToastMessage> Items
    {
        get
        {
            lock (_gate)
                return _items.ToList();
        }
    }

    public void Success(string message) => Show(message, ToastKind.Success);

    public void Error(string message) => Show(message, ToastKind.Error);

    public void Info(string message) => Show(message, ToastKind.Info);

    public void Show(string message, ToastKind kind)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var item = new ToastMessage { Text = message.Trim(), Kind = kind };
        lock (_gate)
            _items.Add(item);

        Changed?.Invoke();
        _ = AutoDismissAsync(item.Id);
    }

    public void Dismiss(string id)
    {
        lock (_gate)
            _items.RemoveAll(x => x.Id == id);

        Changed?.Invoke();
    }

    private async Task AutoDismissAsync(string id)
    {
        try
        {
            await Task.Delay(4500);
            Dismiss(id);
        }
        catch
        {
            // Circuit may be gone.
        }
    }
}
