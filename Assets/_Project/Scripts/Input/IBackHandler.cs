namespace PlushieOdyssey.Inputs
{
    public enum InputContext
    {
        None,
        Menu,
        Combat,
        Dialogue,
        Modal
    }

    public interface IBackHandler
    {
        bool TryHandleBack();
    }
}
