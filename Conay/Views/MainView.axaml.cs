using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Conay.Services;
using Conay.ViewModels;

namespace Conay.Views;

public class ScrollToTopMessage() : ValueChangedMessage<bool>(true);

public partial class MainView : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    private static readonly bool IsProton =
        Environment.GetEnvironmentVariable("STEAM_COMPAT_DATA_PATH") is not null;

    private bool _useProtonStyle;

    public MainView() : this(false)
    {
    }

    public MainView(LauncherConfig config) : this(IsProton ^ config.Data.AlternativeBorders)
    {
    }

    private MainView(bool useProtonStyle)
    {
        _useProtonStyle = useProtonStyle;

        InitializeComponent();
        TransparencyLevelHint =
            [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None];
        WeakReferenceMessenger.Default.Register<ScrollToTopMessage>(this, ScrollToTop);
        WeakReferenceMessenger.Default.Register<AlternativeBordersChangedMessage>(this, OnAlternativeBordersChanged);

        Opened += (_, _) => ApplyWindowStyle();
    }

    private void OnAlternativeBordersChanged(object recipient, AlternativeBordersChangedMessage message)
    {
        _useProtonStyle = IsProton ^ message.Value;
        ApplyWindowStyle();
    }

    private void ApplyWindowStyle()
    {
        if (_useProtonStyle)
        {
            WindowDecorations = WindowDecorations.None;
            ExtendClientAreaToDecorationsHint = false;
        }
        else
        {
            WindowDecorations = WindowDecorations.BorderOnly;
            ExtendClientAreaToDecorationsHint = true;
            if (OperatingSystem.IsWindows())
                SetWindowStyle();
        }
    }

    private const int ResizeMargin = 7;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_useProtonStyle || WindowState != WindowState.Normal) return;
        Cursor = GetResizeEdge(e.GetCurrentPoint(this).Position) switch
        {
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.SizeAll),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.SizeAll),
            _ => Cursor.Default
        };
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_useProtonStyle || WindowState != WindowState.Normal) return;
        WindowEdge? edge = GetResizeEdge(e.GetCurrentPoint(this).Position);
        if (edge.HasValue && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(edge.Value, e);
    }

    private WindowEdge? GetResizeEdge(Point p)
    {
        double w = Bounds.Width, h = Bounds.Height;
        bool l = p.X < ResizeMargin, r = p.X > w - ResizeMargin;
        bool t = p.Y < ResizeMargin, b = p.Y > h - ResizeMargin;
        return (l, r, t, b) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null
        };
    }

    private void SetWindowStyle()
    {
        IntPtr? handle = TryGetPlatformHandle()?.Handle;
        if (handle is null) return;
        int borderColor = 0x00222222;
        DwmSetWindowAttribute(handle.Value, 34, ref borderColor, sizeof(int));
    }

    private void ScrollToTop(object recipient, ScrollToTopMessage message)
    {
        Scroller.ScrollToHome();
    }

    private void ToggleMenuCollapse(object sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.ToggleMenuCollapseCommand.Execute(null);
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}