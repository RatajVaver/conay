using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Conay.Views;

public partial class DualInstallWizardView : Window
{
    public DualInstallWizardView()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseWizard(object sender, RoutedEventArgs e) => Close();
}
