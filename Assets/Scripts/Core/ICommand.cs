namespace Scripts.Core
{
    public interface ICommand
    {
        bool CanExecute();
        void Execute();
    }
}
