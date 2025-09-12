using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using HarmonyDebugger.UI.ViewModels;

namespace HarmonyDebugger.UI.Behaviors;

// Attached helpers to allow clicking an entire CommitRow visual to toggle expansion and keyboard Enter/Space.
public static class CommitRowToggleBehavior
{
    public static readonly AttachedProperty<bool> IsCommitToggleHotspotProperty =
        AvaloniaProperty.RegisterAttached<object, Control, bool>(
            "IsCommitToggleHotspot", false);

    public static void SetIsCommitToggleHotspot(AvaloniaObject element, bool value) => element.SetValue(IsCommitToggleHotspotProperty, value);
    public static bool GetIsCommitToggleHotspot(AvaloniaObject element) => element.GetValue(IsCommitToggleHotspotProperty);

    public static readonly AttachedProperty<bool> EnableKeyboardCommitToggleProperty =
        AvaloniaProperty.RegisterAttached<object, InputElement, bool>(
            "EnableKeyboardCommitToggle", false);

    public static void SetEnableKeyboardCommitToggle(AvaloniaObject element, bool value) => element.SetValue(EnableKeyboardCommitToggleProperty, value);
    public static bool GetEnableKeyboardCommitToggle(AvaloniaObject element) => element.GetValue(EnableKeyboardCommitToggleProperty);

    static CommitRowToggleBehavior()
    {
        IsCommitToggleHotspotProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is Control ctl)
            {
                if (args.NewValue.GetValueOrDefault<bool>())
                {
                    ctl.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
                }
                else
                {
                    ctl.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
                }
            }
        });

        EnableKeyboardCommitToggleProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is InputElement ie)
            {
                if (args.NewValue.GetValueOrDefault<bool>())
                {
                    ie.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
                }
                else
                {
                    ie.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
                }
            }
        });
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        if (sender is not ILogical logical) return;
        // If the original source was a Button (the glyph toggle button), skip here to avoid double toggle.
        if (e.Source is Control ctrl && ctrl is Button)
            return;
        var rowVm = FindRowViewModel(logical);
        if (rowVm is CommitRow commitRow)
        {
            commitRow.ToggleCommand?.Execute(null);
            e.Handled = true;
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Space) return;
        if (sender is not ILogical logical) return;
        if (logical is ListBox lb && lb.DataContext is MainWindowViewModel vm)
        {
            if (vm.SelectedRow is CommitRow commitRow)
            {
                commitRow.ToggleCommand?.Execute(null);
                e.Handled = true;
            }
        }
    }

    private static ICommitListRow? FindRowViewModel(ILogical start)
    {
        foreach (var ancestor in start.GetLogicalAncestors())
        {
            if (ancestor is Control c && c.DataContext is ICommitListRow row)
                return row;
        }
        if (start is Control self && self.DataContext is ICommitListRow row2)
            return row2;
        return null;
    }
}
