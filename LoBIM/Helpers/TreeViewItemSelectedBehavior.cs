using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using TreeView = System.Windows.Controls.TreeView;

namespace LoBIM.Helpers
{
    public static class TreeViewItemSelectedBehavior
    {
        // Attached property to bind the command
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached(
                "Command",
                typeof(ICommand),
                typeof(TreeViewItemSelectedBehavior),
                new PropertyMetadata(null, OnCommandChanged));

        public static ICommand GetCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(CommandProperty);
        }

        public static void SetCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(CommandProperty, value);
        }

        // Handle the CommandProperty changes
        private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TreeView treeView)
            {
                treeView.SelectedItemChanged -= OnTreeViewItemSelectedChanged;
                treeView.SelectedItemChanged += OnTreeViewItemSelectedChanged;
            }
        }

        // Handle the selection change event and execute the command
        private static void OnTreeViewItemSelectedChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var treeView = sender as TreeView;
            var command = GetCommand(treeView);
            if (command != null && command.CanExecute(e.NewValue))
            {
                command.Execute(e.NewValue);
            }
        }
    }
}
