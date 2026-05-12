using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReLPC.ViewModels;

namespace ReLPC;

/// <summary>
/// Maps <c>ReLPC.ViewModels.SignUpWindowViewModel</c> → <c>ReLPC.SignUpWindow</c>.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var vmType = param.GetType();
        var vmName = vmType.Name;
        const string suffix = "ViewModel";
        if (!vmName.EndsWith(suffix, StringComparison.Ordinal))
            return new TextBlock { Text = $"Not Found: {vmType.FullName}" };

        var viewShortName = vmName[..^suffix.Length];
        var ns = vmType.Namespace ?? string.Empty;
        const string vmNs = ".ViewModels";
        var rootNs = ns.EndsWith(vmNs, StringComparison.Ordinal)
            ? ns[..^vmNs.Length]
            : ns;
        var qualified = string.IsNullOrEmpty(rootNs) ? viewShortName : $"{rootNs}.{viewShortName}";

        var assembly = Assembly.GetExecutingAssembly();
        var type = assembly.GetType(qualified)
            ?? Type.GetType($"{qualified}, {assembly.FullName}", throwOnError: false);

        if (type != null && typeof(Control).IsAssignableFrom(type))
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + qualified };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}