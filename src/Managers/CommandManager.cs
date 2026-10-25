namespace AlpacaEngine.Managers;

public interface ICommand
{
    public void Do();
    public void Undo();
}

public sealed class ChangeValueCommand<T>(Action<T> setValue, T oldValue, T newValue) : ICommand
{
    public void Do() => setValue(newValue);

    public void Undo() => setValue(oldValue);
}

public static class CommandManager
{
    private static readonly Stack<ICommand> Commands = new();
    private static readonly Stack<ICommand> RedoCommands = new();

    public static bool CanUndo => Commands.Count > 0;
    public static bool CanRedo => RedoCommands.Count > 0;

    public static void Do(ICommand command)
    {
        command.Do();
        Record(command);
    }

    // Inspector and Gizmo have already applied their preview when the edit ends.
    public static void Record(ICommand command)
    {
        Commands.Push(command);
        RedoCommands.Clear();
    }

    public static void RecordChange<T>(Action<T> setValue, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            Record(new ChangeValueCommand<T>(setValue, oldValue, newValue));
    }

    public static void Undo()
    {
        if (!Commands.TryPeek(out var latestCommand)) return;

        latestCommand.Undo();
        Commands.Pop();
        RedoCommands.Push(latestCommand);
    }

    public static void Redo()
    {
        if (!RedoCommands.TryPeek(out var command)) return;

        command.Do();
        RedoCommands.Pop();
        Commands.Push(command);
    }

    public static void Clear()
    {
        Commands.Clear();
        RedoCommands.Clear();
    }
}
