using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;

        var vmName = data.GetType().FullName!;
        var viewName = vmName.Replace(".ViewModels.", ".Views.")
                             .Replace("ViewModel", "View");

        var type = Type.GetType(viewName);
        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = $"View not found: {viewName}" };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
