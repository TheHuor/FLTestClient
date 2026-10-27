using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FLTestClient.Gui;

/// <summary>Inverts a bool value (e.g. for the second option of a segmented switch).</summary>
public sealed class InverseBool : IValueConverter
{
    /// <summary>Shared instance for <c>{x:Static}</c>.</summary>
    public static readonly InverseBool Instance = new();

    /// <summary>Returns the opposite of the value.</summary>
    /// <param name="value">bool.</param>
    /// <param name="targetType">Target type.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Inverted value.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    /// <summary>Returns the opposite of the value.</summary>
    /// <param name="value">bool.</param>
    /// <param name="targetType">Target type.</param>
    /// <param name="parameter">Unused.</param>
    /// <param name="culture">Unused.</param>
    /// <returns>Inverted value.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>Attached properties for small usability helpers in XAML.</summary>
public static class Behaviors
{
    /// <summary>Makes a ListBox scroll to the end when entries are added.</summary>
    public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.RegisterAttached(
        "AutoScroll", typeof(bool), typeof(Behaviors), new PropertyMetadata(false, OnAutoScrollChanged));

    /// <summary>Reads <see cref="AutoScrollProperty"/>.</summary>
    /// <param name="o">ListBox.</param>
    /// <returns>True if active.</returns>
    public static bool GetAutoScroll(DependencyObject o) => (bool)o.GetValue(AutoScrollProperty);

    /// <summary>Sets <see cref="AutoScrollProperty"/>.</summary>
    /// <param name="o">ListBox.</param>
    /// <param name="value">True to enable.</param>
    public static void SetAutoScroll(DependencyObject o, bool value) => o.SetValue(AutoScrollProperty, value);

    private static readonly DependencyProperty ScrollSubscriptionProperty = DependencyProperty.RegisterAttached(
        "ScrollSubscription", typeof(ScrollSubscription), typeof(Behaviors));

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox list) return;
        if (list.GetValue(ScrollSubscriptionProperty) is not ScrollSubscription subscription)
        {
            subscription = new ScrollSubscription(list);
            list.SetValue(ScrollSubscriptionProperty, subscription);
        }
        subscription.Update();
    }

    private sealed class ScrollSubscription
    {
        private readonly ListBox list;
        private INotifyCollectionChanged? source;
        private readonly System.ComponentModel.DependencyPropertyDescriptor descriptor;
        public ScrollSubscription(ListBox list)
        {
            this.list = list;
            descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListBox));
            list.Loaded += (_, _) => { descriptor.RemoveValueChanged(list, SourceChanged); descriptor.AddValueChanged(list, SourceChanged); Update(); };
            list.Unloaded += (_, _) => { descriptor.RemoveValueChanged(list, SourceChanged); Detach(); };
            if (list.IsLoaded) descriptor.AddValueChanged(list, SourceChanged);
        }
        private void SourceChanged(object? sender, EventArgs e) => Update();
        public void Update()
        {
            Detach();
            if (!list.IsLoaded || !GetAutoScroll(list)) return;
            source = list.ItemsSource as INotifyCollectionChanged;
            if (source != null) source.CollectionChanged += Changed;
        }
        private void Detach()
        {
            if (source != null) source.CollectionChanged -= Changed;
            source = null;
        }
        private void Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && list.Items.Count > 0)
                list.ScrollIntoView(list.Items[^1]);
        }
    }

    /// <summary>
    /// Bindable password for <see cref="PasswordBox"/> (its Password property cannot be bound).
    /// Only meant for local test passwords.
    /// </summary>
    public static readonly DependencyProperty BoundPasswordProperty = DependencyProperty.RegisterAttached(
        "BoundPassword", typeof(string), typeof(Behaviors),
        // Default null: the first binding to "" then also triggers the callback and attaches the handler.
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    /// <summary>Reads <see cref="BoundPasswordProperty"/>.</summary>
    /// <param name="o">PasswordBox.</param>
    /// <returns>Password.</returns>
    public static string GetBoundPassword(DependencyObject o) => (string?)o.GetValue(BoundPasswordProperty) ?? "";

    /// <summary>Sets <see cref="BoundPasswordProperty"/>.</summary>
    /// <param name="o">PasswordBox.</param>
    /// <param name="value">Password.</param>
    public static void SetBoundPassword(DependencyObject o, string value) => o.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
            return;
        box.PasswordChanged -= OnPasswordChanged;
        string value = e.NewValue as string ?? "";
        if (box.Password != value)
            box.Password = value;
        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        SetBoundPassword(box, box.Password);
    }
}
