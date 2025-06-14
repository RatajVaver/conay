using System;
using Conay.ViewModels;

namespace Conay.Factories;

public class PageFactory(Func<Type, PageViewModel> factory)
{
    public PageViewModel GetPageViewModel<T>(Action<T>? afterCreation = null)
        where T : PageViewModel
    {
        PageViewModel viewModel = factory(typeof(T));
        afterCreation?.Invoke((T)viewModel);
        return viewModel;
    }
}