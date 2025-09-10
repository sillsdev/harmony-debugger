using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using HarmonyDebugger.UI.ViewModels;

namespace HarmonyDebugger.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    // Enable drag & drop (older Avalonia builds need attached property set in code)
    DragDrop.SetAllowDrop(this, true);
    // Wire drag & drop events programmatically
    this.AddHandler(DragDrop.DragOverEvent, Window_OnDragOver, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);
    this.AddHandler(DragDrop.DropEvent, Window_OnDrop, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    private void Window_OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles();
            if (files?.Any(f => f.Path.LocalPath.EndsWith(".sqlite", System.StringComparison.OrdinalIgnoreCase) || f.Path.LocalPath.EndsWith(".db", System.StringComparison.OrdinalIgnoreCase)) == true)
            {
                e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
                return;
            }
        }
        e.DragEffects = DragDropEffects.None;
    e.Handled = true;
    }

    private void Window_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.Data.Contains(DataFormats.Files))
        {
            var file = e.Data.GetFiles()?.FirstOrDefault(f => f.Path.LocalPath.EndsWith(".sqlite", System.StringComparison.OrdinalIgnoreCase) || f.Path.LocalPath.EndsWith(".db", System.StringComparison.OrdinalIgnoreCase));
            var path = file?.Path.LocalPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                vm.SetDatabasePath(path);
                e.Handled = true;
            }
        }
    }
}