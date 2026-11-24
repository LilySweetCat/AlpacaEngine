using AlpacaEngine.Managers;

namespace AlpacaEngine.Commands;

public sealed class ChangeValueCommand<T>(Action<T> setValue, T oldValue, T newValue) : ICommand
{
    public void Do() => setValue(newValue);

    public void Undo() => setValue(oldValue);
}