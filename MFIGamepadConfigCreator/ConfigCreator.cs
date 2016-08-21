using System.Collections.ObjectModel;
using MFIGamepadFeeder.Gamepads.Configuration;
using MFIGamepadShared.Configuration;
using vXbox;

/**
// HID Descriptor definitions - Axes
#define HID_USAGE_X		0x30
#define HID_USAGE_Y		0x31
#define HID_USAGE_Z		0x32
#define HID_USAGE_RX	0x33
#define HID_USAGE_RY	0x34
#define HID_USAGE_RZ	0x35
#define HID_USAGE_SL0	0x36
#define HID_USAGE_SL1	0x37
#define HID_USAGE_WHL	0x38
#define HID_USAGE_POV	0x39

 XINPUT defined constants
    //
// Constants for gamepad buttons
//
#define XINPUT_GAMEPAD_DPAD_UP          0x0001
#define XINPUT_GAMEPAD_DPAD_DOWN        0x0002
#define XINPUT_GAMEPAD_DPAD_LEFT        0x0004
#define XINPUT_GAMEPAD_DPAD_RIGHT       0x0008
#define XINPUT_GAMEPAD_START            0x0010
#define XINPUT_GAMEPAD_BACK             0x0020
#define XINPUT_GAMEPAD_LEFT_THUMB       0x0040
#define XINPUT_GAMEPAD_RIGHT_THUMB      0x0080
#define XINPUT_GAMEPAD_LEFT_SHOULDER    0x0100
#define XINPUT_GAMEPAD_RIGHT_SHOULDER   0x0200
#define XINPUT_GAMEPAD_A                0x1000
#define XINPUT_GAMEPAD_B                0x2000
#define XINPUT_GAMEPAD_X                0x4000
#define XINPUT_GAMEPAD_Y                0x8000


//
// Gamepad thresholds
//
#define XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE  7849
#define XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE 8689
#define XINPUT_GAMEPAD_TRIGGER_THRESHOLD    30

     */
namespace MFIGamepadConfigCreator
{
    internal class ConfigCreator
    {
        public GamepadConfiguration GetNimbusConfiguration()
        {
            var _vBox = new IWrapper();
            var configItems = new Collection<GamepadConfigurationItem>
            {
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.Empty
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadUp
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadRight
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadDown
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = null,
                    Type = GamepadItemType.DPadLeft
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_A,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_B,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_X,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_Y,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_LEFT_SHOULDER,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_RIGHT_SHOULDER,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = vXbox.IWrapper.hid_SL0,
                    InvertAxis = false,
                    ConvertAxis = false,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = vXbox.IWrapper.hid_SL1,
                    InvertAxis = false,
                    ConvertAxis = false,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = null,
                    InvertAxis = null,
                    ConvertAxis = null,
                    TargetButtonId = vXbox.IWrapper.xinput_GAMEPAD_START,
                    Type = GamepadItemType.Button
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = vXbox.IWrapper.hid_X,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = vXbox.IWrapper.hid_Y,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = vXbox.IWrapper.hid_RX,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                },
                new GamepadConfigurationItem
                {
                    TargetUsage = vXbox.IWrapper.hid_RY,
                    InvertAxis = false,
                    ConvertAxis = true,
                    TargetButtonId = null,
                    Type = GamepadItemType.Axis
                }
            };

            return new GamepadConfiguration {ConfigItems = configItems};
        }
    }
}