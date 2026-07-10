using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Conay.ViewModels.Parts;

namespace Conay.Views.Parts;

public partial class ModItemView : UserControl
{
    private bool _dragging;
    private ModItemView? _currentTarget;

    public ModItemView()
    {
        InitializeComponent();
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (DataContext is not ModItemViewModel { IsReorderable: true } vm) return;

        _dragging = true;
        vm.IsDragging = true;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void ModItemView_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        Point point = e.GetPosition(topLevel);
        Visual? hit = topLevel.InputHitTest(point) as Visual;
        ModItemView? targetView = hit?.FindAncestorOfType<ModItemView>(includeSelf: true);

        if (targetView == this) targetView = null;
        if (targetView == _currentTarget) return;

        if (_currentTarget?.DataContext is ModItemViewModel previousTarget)
            previousTarget.IsDropTarget = false;

        _currentTarget = targetView;

        if (_currentTarget?.DataContext is ModItemViewModel newTarget)
            newTarget.IsDropTarget = true;
    }

    private void ModItemView_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        CommitDrag();
    }

    private void ModItemView_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CommitDrag();
    }

    private void CommitDrag()
    {
        if (!_dragging) return;
        _dragging = false;

        if (DataContext is ModItemViewModel vm)
            vm.IsDragging = false;

        if (_currentTarget?.DataContext is ModItemViewModel target
            && DataContext is ModItemViewModel dragged)
        {
            target.IsDropTarget = false;
            target.OnDroppedOn?.Invoke(dragged);
        }

        _currentTarget = null;
    }
}
