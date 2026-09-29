using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HidWizards.UCR.Views.Controls
{
    /// <summary>
    /// Press-to-capture key picker: click, then press the physical key you want this control's bound
    /// value to represent. Deliberately not a dropdown/searchable list -- capturing the actual keypress
    /// is more direct and is the same "press what you want to bind" gesture already used everywhere
    /// else in the Patch Bay (Listen for a physical button/axis).
    /// </summary>
    public partial class KeyCaptureControl : UserControl
    {
        public static readonly DependencyProperty CapturedKeyCodeProperty = DependencyProperty.Register(
            nameof(CapturedKeyCode), typeof(ushort), typeof(KeyCaptureControl),
            new FrameworkPropertyMetadata((ushort)0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCapturedKeyCodeChanged));

        public ushort CapturedKeyCode
        {
            get => (ushort)GetValue(CapturedKeyCodeProperty);
            set => SetValue(CapturedKeyCodeProperty, value);
        }

        public static readonly DependencyProperty IsListeningProperty = DependencyProperty.Register(
            nameof(IsListening), typeof(bool), typeof(KeyCaptureControl), new PropertyMetadata(false, OnDisplayInputsChanged));

        public bool IsListening
        {
            get => (bool)GetValue(IsListeningProperty);
            private set => SetValue(IsListeningProperty, value);
        }

        public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(KeyCaptureControl), new PropertyMetadata("Press to set key"));

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        public KeyCaptureControl()
        {
            InitializeComponent();
            UpdateDisplayText();
        }

        private static void OnCapturedKeyCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyCaptureControl)d).UpdateDisplayText();
        }

        private static void OnDisplayInputsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyCaptureControl)d).UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            if (IsListening)
            {
                DisplayText = "Press a key…";
                return;
            }

            DisplayText = CapturedKeyCode == 0
                ? "Press to set key"
                : KeyInterop.KeyFromVirtualKey(CapturedKeyCode).ToString();
        }

        private void CaptureButton_OnClick(object sender, RoutedEventArgs e)
        {
            IsListening = true;
        }

        private void CaptureButton_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsListening) return;

            // Alt-held keys report as Key.System with the real key in SystemKey; every other key
            // (including the modifier keys themselves, pressed alone) reports normally via e.Key.
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.None)
            {
                return;
            }

            CapturedKeyCode = (ushort)KeyInterop.VirtualKeyFromKey(key);
            IsListening = false;
            e.Handled = true;
        }
    }
}
