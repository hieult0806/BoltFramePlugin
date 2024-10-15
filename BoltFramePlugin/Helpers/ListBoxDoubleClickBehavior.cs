using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace BoltFramePlugin.Helpers
{
    public static class ListBoxDoubleClickBehavior
    {
        public static readonly DependencyProperty DoubleClickCommandProperty =
            DependencyProperty.RegisterAttached("DoubleClickCommand", typeof(ICommand), typeof(ListBoxDoubleClickBehavior), new PropertyMetadata(null, OnDoubleClickCommandChanged));

        public static ICommand GetDoubleClickCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(DoubleClickCommandProperty);
        }

        public static void SetDoubleClickCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(DoubleClickCommandProperty, value);
        }

        private static void OnDoubleClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var listBox = d as System.Windows.Controls.ListBox;

            if (listBox != null)
            {
                listBox.MouseDoubleClick -= ListBox_MouseDoubleClick;
                listBox.MouseDoubleClick += ListBox_MouseDoubleClick;
            }
        }

        private static void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as System.Windows.Controls.ListBox;
            if (listBox != null && listBox.SelectedItem != null)
            {
                var command = GetDoubleClickCommand(listBox);
                if (command != null && command.CanExecute(listBox.SelectedItem))
                {
                    command.Execute(listBox.SelectedItem);
                }
            }
        }
    }
}
