namespace BianQian.App.Controls;

public interface IEditorOperations
{
    void ToggleBold();

    void IncreaseFontSize();

    void DecreaseFontSize();

    void Undo();

    void Redo();
}
