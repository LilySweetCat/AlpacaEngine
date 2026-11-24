namespace AlpacaEngine.Commands;

public interface ICommand
{
    public void Do();

    public void Undo();
}