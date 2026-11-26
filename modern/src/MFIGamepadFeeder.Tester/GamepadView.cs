using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MFIGamepadFeeder.Core.Models;

namespace MFIGamepadFeeder.Tester;

public sealed class GamepadView : StackPanel
{
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8));
    private static readonly Brush Surface = new SolidColorBrush(Color.FromRgb(0xF1, 0xF4, 0xF8));
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
    private readonly StickPlot _left = new();
    private readonly StickPlot _right = new();
    private readonly TextBlock _leftValue = new();
    private readonly TextBlock _rightValue = new();
    private readonly TextBlock _leftTriggerValue = new();
    private readonly TextBlock _rightTriggerValue = new();
    private readonly ProgressBar _leftTrigger = new() { Maximum = 255, Height = 9 };
    private readonly ProgressBar _rightTrigger = new() { Maximum = 255, Height = 9 };
    private readonly Dictionary<XInputButtons, Border> _buttons = [];
    private GamepadState? _last;
    private GamepadState? _lastLabels;

    public GamepadView()
    {
        var sticks = new Grid();
        sticks.ColumnDefinitions.Add(new ColumnDefinition());
        sticks.ColumnDefinitions.Add(new ColumnDefinition());
        AddStick(sticks, 0, "Left stick", _left, _leftValue, "Left trigger", _leftTrigger, _leftTriggerValue);
        AddStick(sticks, 1, "Right stick", _right, _rightValue, "Right trigger", _rightTrigger, _rightTriggerValue);
        Children.Add(sticks);

        var buttons = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
        (XInputButtons Button, string Label)[] labels =
        [
            (XInputButtons.A, "A"), (XInputButtons.B, "B"),
            (XInputButtons.X, "X"), (XInputButtons.Y, "Y"),
            (XInputButtons.LBumper, "LB"), (XInputButtons.RBumper, "RB"),
            (XInputButtons.Start, "Menu"), (XInputButtons.Back, "Back"),
            (XInputButtons.LeftStick, "L3"), (XInputButtons.RightStick, "R3"),
            (XInputButtons.DpadUp, "Up"), (XInputButtons.DpadDown, "Down"),
            (XInputButtons.DpadLeft, "Left"), (XInputButtons.DpadRight, "Right"),
        ];
        foreach (var (button, label) in labels)
        {
            var indicator = new Border
            {
                Background = Surface,
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(0, 0, 6, 6),
                MinWidth = 40,
                Child = new TextBlock { Text = label, TextAlignment = TextAlignment.Center, Foreground = Ink },
                ToolTip = label + " released",
            };
            _buttons.Add(button, indicator);
            buttons.Children.Add(indicator);
        }
        Children.Add(buttons);
        Update(GamepadState.Neutral);
    }

    private static void AddStick(Grid grid, int column, string title, StickPlot plot, TextBlock value,
        string triggerTitle, ProgressBar trigger, TextBlock triggerValue)
    {
        var stack = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 10, 0, column == 0 ? 10 : 0, 0) };
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(plot);
        value.FontFamily = new FontFamily("Consolas");
        value.FontSize = 12;
        value.TextWrapping = TextWrapping.Wrap;
        value.TextAlignment = TextAlignment.Center;
        value.Margin = new Thickness(0, 0, 0, 15);
        stack.Children.Add(value);
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        triggerValue.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(triggerValue, Dock.Right);
        row.Children.Add(triggerValue);
        row.Children.Add(new TextBlock { Text = triggerTitle });
        stack.Children.Add(row);
        trigger.Foreground = Accent;
        stack.Children.Add(trigger);
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
    }

    public void Update(GamepadState state, bool updateLabels = true)
    {
        if (_last != state)
        {
            if (_last?.LeftThumbX != state.LeftThumbX || _last?.LeftThumbY != state.LeftThumbY)
                _left.SetPosition(state.LeftThumbX, state.LeftThumbY);
            if (_last?.RightThumbX != state.RightThumbX || _last?.RightThumbY != state.RightThumbY)
                _right.SetPosition(state.RightThumbX, state.RightThumbY);
            if (_last?.LeftTrigger != state.LeftTrigger) _leftTrigger.Value = state.LeftTrigger;
            if (_last?.RightTrigger != state.RightTrigger) _rightTrigger.Value = state.RightTrigger;

            var changedButtons = _last.HasValue ? _last.Value.Buttons ^ state.Buttons : (XInputButtons)ushort.MaxValue;
            foreach (var (button, border) in _buttons)
            {
                if ((changedButtons & button) == 0) continue;
                var pressed = (state.Buttons & button) != 0;
                border.Background = pressed ? Accent : Surface;
                var label = (TextBlock)border.Child;
                label.Foreground = pressed ? Brushes.White : Ink;
                border.ToolTip = $"{label.Text} {(pressed ? "pressed" : "released")}";
            }
            _last = state;
        }

        if (updateLabels && _lastLabels != state)
        {
            if (_lastLabels?.LeftThumbX != state.LeftThumbX || _lastLabels?.LeftThumbY != state.LeftThumbY)
                _leftValue.Text = $"X {state.LeftThumbX,6}   Y {state.LeftThumbY,6}";
            if (_lastLabels?.RightThumbX != state.RightThumbX || _lastLabels?.RightThumbY != state.RightThumbY)
                _rightValue.Text = $"X {state.RightThumbX,6}   Y {state.RightThumbY,6}";
            if (_lastLabels?.LeftTrigger != state.LeftTrigger) _leftTriggerValue.Text = $"{state.LeftTrigger} / 255";
            if (_lastLabels?.RightTrigger != state.RightTrigger) _rightTriggerValue.Text = $"{state.RightTrigger} / 255";
            _lastLabels = state;
        }
    }

    private sealed class StickPlot : FrameworkElement
    {
        private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(0xB6, 0xC1, 0xCF)), 1);
        private short _x;
        private short _y;
        static StickPlot() { GridPen.Freeze(); }
        public StickPlot() { Height = 145; Margin = new Thickness(0, 7, 0, 7); }

        public void SetPosition(short x, short y)
        {
            _x = x;
            _y = y;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            var radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 9);
            dc.DrawEllipse(Surface, null, center, radius, radius);
            dc.DrawLine(GridPen, new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y));
            dc.DrawLine(GridPen, new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius));
            var x = _x / (_x < 0 ? 32768.0 : 32767.0);
            var y = _y / (_y < 0 ? 32768.0 : 32767.0);
            var marker = new Point(center.X + x * (radius - 6), center.Y - y * (radius - 6));
            dc.DrawEllipse(Accent, null, marker, 6, 6);
        }
    }
}
