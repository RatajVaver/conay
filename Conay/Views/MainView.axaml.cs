using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Conay.ViewModels;

namespace Conay.Views;

public class ScrollToTopMessage() : ValueChangedMessage<bool>(true);

public partial class MainView : Window
{
    public MainView()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<ScrollToTopMessage>(this, ScrollToTop);
    }

    private void ScrollToTop(object recipient, ScrollToTopMessage message)
    {
        Scroller.ScrollToHome();
    }

    private void ToggleMenuCollapse(object sender, RoutedEventArgs e)
    {
        (DataContext as MainViewModel)?.ToggleMenuCollapseCommand.Execute(null);
    }
}