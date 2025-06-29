using Avalonia;
using Avalonia.Controls;
using Conay.ViewModels;

namespace Conay.Behaviors;

public class VisibilityHandler
{
    private static readonly AttachedProperty<bool> LazyLoad =
        AvaloniaProperty.RegisterAttached<Control, bool>("LazyLoad", typeof(VisibilityHandler), defaultValue: false);

    public static bool GetLazyLoad(Control control) =>
        control.GetValue(LazyLoad);

    public static void SetLazyLoad(Control control, bool value) =>
        control.SetValue(LazyLoad, value);

    static VisibilityHandler()
    {
        LazyLoad.Changed.AddClassHandler<Control>((control, e) =>
        {
            HandlePropertyChange(control, e.GetNewValue<bool>());
        });
    }

    private static void HandlePropertyChange(Control control, bool isEnabled)
    {
        if (isEnabled)
        {
            control.AttachedToVisualTree += OnControlAttached;
            control.DetachedFromVisualTree += OnControlDetached;
        }
        else
        {
            control.AttachedToVisualTree -= OnControlAttached;
            control.DetachedFromVisualTree -= OnControlDetached;
        }
    }

    private static void OnControlAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control { DataContext: ILazyLoad vm })
        {
            vm.IsVisible = true;

            if (!vm.IsLoaded)
            {
                _ = vm.LoadDataAsync();
            }
        }
    }

    private static void OnControlDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control { DataContext: ILazyLoad vm })
        {
            vm.IsVisible = false;
        }
    }
}