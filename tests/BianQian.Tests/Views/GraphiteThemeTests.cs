using BianQian.App;
using BianQian.App.Controls;
using BianQian.App.Views;
using FluentAssertions;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace BianQian.Tests.Views;

public sealed class GraphiteThemeTests
{
    private static readonly IReadOnlyDictionary<string, Color> ExpectedPalette =
        new Dictionary<string, Color>
        {
            ["GraphiteWindowColor"] = Color.FromRgb(0x1C, 0x1C, 0x1E),
            ["GraphiteEditorColor"] = Color.FromRgb(0x24, 0x24, 0x26),
            ["GraphiteToolbarColor"] = Color.FromRgb(0x2C, 0x2C, 0x2E),
            ["GraphiteBorderColor"] = Color.FromRgb(0x3A, 0x3A, 0x3C),
            ["GraphitePrimaryTextColor"] = Color.FromRgb(0xF5, 0xF5, 0xF7),
            ["GraphiteSecondaryTextColor"] = Color.FromRgb(0xA1, 0xA1, 0xA6),
            ["GraphiteAccentColor"] = Color.FromRgb(0x0A, 0x84, 0xFF),
            ["GraphiteDangerColor"] = Color.FromRgb(0xFF, 0x45, 0x3A),
        };

    [Fact]
    public void Theme_dictionary_exposes_the_exact_eight_color_palette()
    {
        RunInSta(() =>
        {
            var theme = LoadTheme();
            var palette = theme.Keys
                .Cast<object>()
                .Where(key => theme[key] is Color)
                .ToDictionary(key => key.ToString()!, key => (Color)theme[key]);

            palette.Should().BeEquivalentTo(ExpectedPalette);
        });
    }

    [Fact]
    public void Main_window_retains_exactly_seven_toolbar_command_buttons()
    {
        RunInSta(() =>
        {
            var window = new MainWindow();
            window.Resources.MergedDictionaries.Add(LoadTheme());

            var toolbar = window.FindName("BottomToolbar").Should().BeOfType<UniformGrid>().Subject;
            toolbar.Children.OfType<Button>().Should().HaveCount(7);
            toolbar.Children.OfType<Button>().Should().OnlyContain(button =>
                BindingOperations.IsDataBound(button, ButtonBase.CommandProperty));
        });
    }

    [Fact]
    public void Task_date_and_editor_views_resolve_dynamic_graphite_resources()
    {
        RunInSta(() =>
        {
            var dateView = WithTheme(new DateSectionView());
            var taskView = WithTheme(new TaskBlockView());
            var editorView = WithTheme(new RichTextEditor());

            var dateHeader = dateView.FindName("DateHeaderText").Should().BeOfType<TextBlock>().Subject;
            var taskText = taskView.FindName("TaskText").Should().BeOfType<TextBox>().Subject;
            var editor = editorView.FindName("Editor").Should().BeOfType<RichTextBox>().Subject;
            var editorSurface = editorView.FindName("EditorSurface").Should().BeOfType<Border>().Subject;

            AssertDynamicBrush(dateHeader, TextBlock.ForegroundProperty, ExpectedPalette["GraphiteSecondaryTextColor"]);
            AssertDynamicBrush(taskText, TextBox.ForegroundProperty, ExpectedPalette["GraphitePrimaryTextColor"]);
            AssertDynamicBrush(editor, RichTextBox.ForegroundProperty, ExpectedPalette["GraphitePrimaryTextColor"]);
            AssertDynamicBrush(editorSurface, Border.BackgroundProperty, ExpectedPalette["GraphiteEditorColor"]);
        });
    }

    private static T WithTheme<T>(T element) where T : FrameworkElement
    {
        element.Resources.MergedDictionaries.Add(LoadTheme());
        return element;
    }

    private static void AssertDynamicBrush(
        DependencyObject element,
        DependencyProperty property,
        Color expectedColor)
    {
        var source = DependencyPropertyHelper.GetValueSource(element, property);
        source.IsExpression.Should().BeTrue($"{property.Name} must use a DynamicResource");
        element.GetValue(property).Should().BeOfType<SolidColorBrush>()
            .Which.Color.Should().Be(expectedColor);
    }

    private static ResourceDictionary LoadTheme()
    {
        _ = Application.ResourceAssembly;
        return new()
        {
            Source = new Uri(
                "/BianQian.App;component/Themes/GraphiteTheme.xaml",
                UriKind.Relative),
        };
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
