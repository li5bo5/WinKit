using System;
using System.Windows;
using System.Windows.Input;

namespace WinKit.Common
{
    /// <summary>
    /// 提供在 XAML 中声明式绑定单击与双击行为的附加属性
    /// </summary>
    public static class ClickBehavior
    {
        public static readonly DependencyProperty SingleCommandProperty =
            DependencyProperty.RegisterAttached(
                "SingleCommand",
                typeof(ICommand),
                typeof(ClickBehavior),
                new PropertyMetadata(null));

        public static readonly DependencyProperty DoubleCommandProperty =
            DependencyProperty.RegisterAttached(
                "DoubleCommand",
                typeof(ICommand),
                typeof(ClickBehavior),
                new PropertyMetadata(null));

        public static void SetSingleCommand(DependencyObject obj, ICommand value)
            => obj.SetValue(SingleCommandProperty, value);

        public static ICommand? GetSingleCommand(DependencyObject obj)
            => (ICommand?)obj.GetValue(SingleCommandProperty);

        public static void SetDoubleCommand(DependencyObject obj, ICommand value)
            => obj.SetValue(DoubleCommandProperty, value);

        public static ICommand? GetDoubleCommand(DependencyObject obj)
            => (ICommand?)obj.GetValue(DoubleCommandProperty);
    }
}
