using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;

namespace BianQian.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BianQian");

    public static string DatabasePath { get; } = Path.Combine(DataDirectory, "bianqian.db");
}

