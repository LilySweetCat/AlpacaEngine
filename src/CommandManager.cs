using System.Numerics;

namespace AlpacaEngine;

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

    public static void Do(ICommand command)
    {
        command.Do();
        Commands.Push(command);
    }

    public static void Undo()
    {
        var latestCommand = Commands.Pop();
        latestCommand.Undo();
    }
}