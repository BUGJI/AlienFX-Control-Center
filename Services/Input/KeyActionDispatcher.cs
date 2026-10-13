using AlienFXControlCenter.Models;
using AlienFXControlCenter.Services;

namespace AlienFXControlCenter.Services.Input;

/// <summary>Feedback raised after a bound key fires, for the shell to surface.</summary>
public sealed class ActionFeedback
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool IsError { get; init; }
}

/// <summary>
/// Subscribes to recognized key presses and runs their bound action. Started
/// once at app launch; actions are applied through <see cref="TemplateApplier"/>.
/// </summary>
public sealed class KeyActionDispatcher
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _suspendCount;
    private bool _started;

    public static KeyActionDispatcher Current { get; } = new();

    public event Action<ActionFeedback>? Message;

    public void Start()
    {
        if (_started)
            return;

        _started = true;
        InputService.Current.Recognized += OnRecognized;
        Log.Write("KeyActionDispatcher started.");
    }

    /// <summary>Pauses dispatch (e.g. while the key-capture dialog is open).</summary>
    public void Suspend() => Interlocked.Increment(ref _suspendCount);

    public void Resume()
    {
        if (Interlocked.Decrement(ref _suspendCount) < 0)
            Interlocked.Exchange(ref _suspendCount, 0);
    }

    private bool IsSuspended => Volatile.Read(ref _suspendCount) > 0;

    private void OnRecognized(NamedKey key)
    {
        if (IsSuspended || !key.Enabled)
            return;

        if (key.Action is not { Kind: KeyActionKind.ApplyTemplate } action)
            return;

        _ = DispatchAsync(key, action);
    }

    private async Task DispatchAsync(NamedKey key, KeyAction action)
    {
        // Ignore rapid repeats while a previous apply is still running.
        if (!await _gate.WaitAsync(0))
            return;

        try
        {
            var template = ResolveTemplate(action);
            if (template is null)
            {
                Message?.Invoke(new ActionFeedback
                {
                    Title = Localizer.Get("Keys_Title.Text"),
                    Message = Localizer.Format("Keys_TemplateMissing", action.TemplateName ?? string.Empty),
                    IsError = true,
                });
                return;
            }

            var outcome = await TemplateApplier.ApplyAsync(template);
            var text = TemplateApplier.FormatStatus(template, outcome);
            var isError = outcome.FxMissing || outcome.FanMissing;

            Message?.Invoke(new ActionFeedback
            {
                Title = Localizer.Get("Keys_ActionApplyTemplate"),
                Message = text,
                IsError = isError,
            });
        }
        catch (Exception ex)
        {
            Log.Write($"Key action failed: {ex.Message}");
            Message?.Invoke(new ActionFeedback
            {
                Title = Localizer.Get("Keys_Title.Text"),
                Message = Localizer.Format("Keys_ApplyFailed", ex.Message),
                IsError = true,
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private static LightingTemplate? ResolveTemplate(KeyAction action)
    {
        LightingTemplate? template = null;
        if (Guid.TryParse(action.TemplateId, out var id))
            template = TemplateStore.GetById(id);

        if (template is null && !string.IsNullOrWhiteSpace(action.TemplateName))
        {
            // Legacy bindings may hold an id that was regenerated because the
            // template predated persisted ids. Fall back to the cached display
            // name and heal the stored id so the binding survives reloads.
            template = TemplateStore.Get(action.TemplateName);
            if (template is not null)
            {
                action.TemplateId = template.Id.ToString();
                KeyStore.Save();
            }
        }

        if (template is not null)
            action.TemplateName = template.Name;
        return template;
    }
}
