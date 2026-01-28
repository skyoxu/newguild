namespace Game.Core.Services;

public sealed class AsyncInteractionState
{
    private enum Mode
    {
        Disabled,
        Loading,
        Error,
    }

    private readonly Mode _mode;

    private AsyncInteractionState(Mode mode, string? errorMessage)
    {
        _mode = mode;
        ErrorMessage = errorMessage;
    }

    public bool IsLoading => _mode == Mode.Loading;

    public bool CanRetry => _mode == Mode.Error;

    public string? ErrorMessage { get; }

    public static AsyncInteractionState Loading() => new(Mode.Loading, errorMessage: null);

    public static AsyncInteractionState Disabled() => new(Mode.Disabled, errorMessage: null);

    public static AsyncInteractionState Error(string errorMessage)
    {
        if (errorMessage is null)
            throw new System.ArgumentNullException(nameof(errorMessage));
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new System.ArgumentException("Error message must not be empty.", nameof(errorMessage));

        return new AsyncInteractionState(Mode.Error, errorMessage);
    }

    public static AsyncInteractionState Error(System.Exception ex)
    {
        if (ex is null)
            throw new System.ArgumentNullException(nameof(ex));

        var msg = ex.Message;
        if (string.IsNullOrWhiteSpace(msg))
            msg = ex.GetType().Name;

        return Error(msg);
    }

    public AsyncInteractionState Retry() => CanRetry ? Loading() : this;
}
