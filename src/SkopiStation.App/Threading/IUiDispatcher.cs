using System.Windows.Threading;

namespace SkopiStation.App.Threading;

/// <summary>
/// Moves work onto the UI thread. Abstracted so ViewModels can be tested without a WPF
/// <see cref="Dispatcher"/>, which does not exist in a unit test host.
/// </summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action)
    {
        // Already on the UI thread: run inline rather than queue, so an operation started from a
        // command finishes before the command returns.
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.InvokeAsync(action);
    }
}
