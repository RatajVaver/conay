using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Conay.ViewModels;

namespace Conay;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        string viewName = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.InvariantCulture);
        Type? type = Type.GetType(viewName);

        if (type is null)
            return null;

        Control control = (Control)Activator.CreateInstance(type)!;
        control.DataContext = data;

        return control;
    }

    public bool Match(object? data) => data is ViewModelBase;
}