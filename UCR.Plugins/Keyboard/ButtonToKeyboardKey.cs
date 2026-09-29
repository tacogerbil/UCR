using HidWizards.UCR.Core.Attributes;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Core.Models.Binding;
using HidWizards.UCR.Core.Utilities;

namespace HidWizards.UCR.Plugins.Keyboard
{
    // Same shape as "Button to Filter" (UCR.Plugins/Filter/ButtonToFilter.cs): a side-effecting
    // plugin with no PluginOutput at all, since sending a keystroke doesn't target any output-device
    // slot. Lives in the Patch Bay's "Keyboard Shortcuts" panel alongside Filters / Modifier Buttons,
    // not as a separate output device -- there is exactly one output device the user ever sees
    // (their real controller), and this is just another kind of side effect a button can trigger.
    [Plugin("Button to Keyboard Key", Group = "Keyboard", Description = "Send a keyboard key press while a button is held")]
    [PluginInput(DeviceBindingCategory.Momentary, "Button")]
    public class ButtonToKeyboardKey : Plugin
    {
        // Not [PluginGui]-tagged: set via the bespoke KeyCaptureControl (press-to-capture), not the
        // generic reflection-based property list, since a raw Win32 virtual-key code is meaningless
        // to type by hand. Still persists normally -- plugin serialization isn't limited to
        // [PluginGui] properties.
        public ushort KeyCode { get; set; }

        private bool _isDown;

        public override void Update(params short[] values)
        {
            var down = values[0] != 0;
            // Edge-detected, unlike a real output slot's SetButtonState (which just re-applies the
            // current state every tick, harmless for a report byte) -- SendInput injects a discrete
            // keystroke, so re-sending it every poll tick would flood the target with key-repeat far
            // faster than any real keyboard, which is exactly wrong for menu navigation.
            if (down == _isDown) return;
            _isDown = down;
            if (KeyCode != 0) NativeKeyboardInput.SendKey(KeyCode, down);
        }
    }
}
