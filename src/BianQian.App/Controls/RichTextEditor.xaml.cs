using BianQian.App.ViewModels;
using BianQian.App.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace BianQian.App.Controls;

public partial class RichTextEditor : UserControl, IEditorOperations
{
    private const double FontStep = 2;
    private bool _isSynchronizing;

    public RichTextEditor()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Editor.TextChanged += OnTextChanged;
        Editor.GotKeyboardFocus += OnGotKeyboardFocus;
    }

    public void ToggleBold()
    {
        EditingCommands.ToggleBold.Execute(null, Editor);
        Editor.Focus();
    }

    public void IncreaseFontSize() => ChangeFontSize(FontStep);

    public void DecreaseFontSize() => ChangeFontSize(-FontStep);

    public void Undo()
    {
        if (Editor.CanUndo)
        {
            Editor.Undo();
        }
    }

    public void Redo()
    {
        if (Editor.CanRedo)
        {
            Editor.Redo();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => LoadDocument();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadDocument();
        if (DataContext is TextBlockViewModel { ShouldReceiveFocus: true } viewModel)
        {
            Editor.Focus();
            viewModel.MarkFocusReceived();
        }
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is not MainWindowViewModel mainViewModel)
        {
            return;
        }

        mainViewModel.SetActiveEditor(this);
        var sectionView = FindAncestor<DateSectionView>(this);
        if (sectionView?.DataContext is DateSectionViewModel section)
        {
            mainViewModel.SetActiveSection(section);
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizing || DataContext is not TextBlockViewModel viewModel)
        {
            return;
        }

        using var stream = new MemoryStream();
        new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd)
            .Save(stream, DataFormats.Rtf);
        viewModel.Text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private void LoadDocument()
    {
        if (!IsLoaded || DataContext is not TextBlockViewModel viewModel)
        {
            return;
        }

        _isSynchronizing = true;
        try
        {
            var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
            if (viewModel.Text.TrimStart().StartsWith(@"{\rtf", StringComparison.Ordinal))
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(viewModel.Text));
                range.Load(stream, DataFormats.Rtf);
            }
            else
            {
                range.Text = viewModel.Text;
            }
        }
        catch (ArgumentException)
        {
            new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text = viewModel.Text;
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void ChangeFontSize(double delta)
    {
        var value = Editor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
        var current = value is double size ? size : Editor.FontSize;
        var next = Math.Clamp(current + delta, 8, 72);
        Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, next);
        Editor.Focus();
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        for (var current = VisualTreeHelper.GetParent(start); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
