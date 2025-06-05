using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace data_sentry.Views;

using data_sentry.ViewModels;

public partial class ChecksView : UserControl
{
    public ChecksView()
    {
        InitializeComponent();

        // Register for attached/detached events
        AttachedToLogicalTree += OnAttachedToLogicalTree;
        DetachedFromLogicalTree += OnDetachedFromLogicalTree;
    }

    private void OnAttachedToLogicalTree(object sender, LogicalTreeAttachmentEventArgs e)
    {
        // Find parent TabControl if this control is inside a TabItem
        var parent = this.FindLogicalAncestorOfType<TabControl>();
        if (parent != null)
        {
            parent.SelectionChanged += OnTabSelectionChanged;
        }

        // Also load initially if we're visible on startup
        if (IsEffectivelyVisible && DataContext is ChecksViewModel viewModel)
        {
            viewModel.LoadConfig();
        }
    }

    private void OnDetachedFromLogicalTree(object sender, LogicalTreeAttachmentEventArgs e)
    {
        // Unsubscribe when detached to prevent memory leaks
        var parent = this.FindLogicalAncestorOfType<TabControl>();
        if (parent != null)
        {
            parent.SelectionChanged -= OnTabSelectionChanged;
        }
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Check if our tab is now selected
        if (IsEffectivelyVisible && DataContext is ChecksViewModel viewModel)
        {
            viewModel.LoadChecks();
        }
    }

}