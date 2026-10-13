using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Geteilt.Api.Tests.Diagnostics;

/// <summary>
/// Reports the SQL EF Core actually issues, one callback per command.
/// </summary>
/// <remarks>
/// Subscribes to EF's own diagnostic source rather than inspecting the LINQ, because the failure
/// this guards against is invisible in the LINQ: a projection that cannot be translated falls back
/// to client evaluation and quietly issues one query per row. Only the database can say how many
/// round trips really happened.
/// </remarks>
internal static class CommandCounter
{
    internal static IDisposable Start(Action<string> onCommand) => new Subscription(onCommand);

    private sealed class Subscription : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly Action<string> _onCommand;
        private readonly IDisposable _listeners;
        private IDisposable? _commands;

        internal Subscription(Action<string> onCommand)
        {
            _onCommand = onCommand;
            _listeners = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == DbLoggerCategory.Name)
            {
                _commands = listener.Subscribe(new Commands(_onCommand));
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void Dispose()
        {
            _commands?.Dispose();
            _listeners.Dispose();
        }
    }

    private sealed class Commands(Action<string> onCommand) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == RelationalEventId.CommandExecuting.Name
                && value.Value is CommandEventData command)
            {
                onCommand(command.Command.CommandText);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
