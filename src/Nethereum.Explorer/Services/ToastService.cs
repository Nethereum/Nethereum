namespace Nethereum.Explorer.Services;

public class ToastService
{
    private readonly List<ToastMessage> _toasts = new();
    private readonly object _lock = new();

    public IReadOnlyList<ToastMessage> Toasts
    {
        get
        {
            lock (_lock)
            {
                return _toasts.ToList();
            }
        }
    }

    public event Action? OnChange;

    public void ShowSuccess(string message) => Show(message, ToastType.Success);
    public void ShowError(string message) => Show(message, ToastType.Error);
    public void ShowInfo(string message) => Show(message, ToastType.Info);
    public void ShowWarning(string message) => Show(message, ToastType.Warning);

    public void Show(string message, ToastType type, int durationMs = 4000)
    {
        var toast = new ToastMessage { Id = Guid.NewGuid(), Message = message, Type = type, DurationMs = durationMs };
        lock (_lock) { _toasts.Add(toast); }
        OnChange?.Invoke();
        _ = RemoveAfterDelayAsync(toast);
    }

    public void Remove(Guid id)
    {
        lock (_lock) { _toasts.RemoveAll(t => t.Id == id); }
        OnChange?.Invoke();
    }

    private async Task RemoveAfterDelayAsync(ToastMessage toast)
    {
        try
        {
            await Task.Delay(toast.DurationMs);
            lock (_lock) { _toasts.Remove(toast); }
            OnChange?.Invoke();
        }
        catch (ObjectDisposedException) { }
    }

    public class ToastMessage
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = "";
        public ToastType Type { get; set; }
        public int DurationMs { get; set; }
    }

    public enum ToastType { Success, Error, Info, Warning }
}
