using BianQian.App.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BianQian.App.Views;

public partial class DateSectionView : UserControl
{
    public DateSectionView()
    {
        InitializeComponent();
        PreviewGotKeyboardFocus += OnPreviewGotKeyboardFocus;
    }

    private void OnPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is DateSectionViewModel section
            && Window.GetWindow(this)?.DataContext is MainWindowViewModel mainViewModel)
        {
            mainViewModel.SetActiveSection(section);
        }
    }
}
