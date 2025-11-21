using System.Windows.Input;
using System.Windows;

namespace LoBIM.Helpers
{
    public static class MouseClickBehavior
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(MouseClickBehavior), new PropertyMetadata(null, OnCommandChanged));

        public static void SetCommand(UIElement element, ICommand value)
        {
            element.SetValue(CommandProperty, value);
        }

        public static ICommand GetCommand(UIElement element)
        {
            return (ICommand)element.GetValue(CommandProperty);
        }

        private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                element.MouseLeftButtonDown -= Element_MouseLeftButtonDown;
                if (e.NewValue != null)
                {
                    element.MouseLeftButtonDown += Element_MouseLeftButtonDown;
                }
            }
        }

        private static void Element_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            var command = GetCommand(element);

            if (command != null && command.CanExecute(null))
            {
                command.Execute(null);
            }
        }
    }
}
