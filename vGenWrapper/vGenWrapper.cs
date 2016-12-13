using System.Runtime.InteropServices;
using System.Text;

namespace vGenWrapper
{
//
//#define XINPUT_GAMEPAD_DPAD_UP          0x0001
//#define XINPUT_GAMEPAD_DPAD_DOWN        0x0002
//#define XINPUT_GAMEPAD_DPAD_LEFT        0x0004
//#define XINPUT_GAMEPAD_DPAD_RIGHT       0x0008
//#define XINPUT_GAMEPAD_START            0x0010
//#define XINPUT_GAMEPAD_BACK             0x0020
//#define XINPUT_GAMEPAD_LEFT_THUMB       0x0040
//#define XINPUT_GAMEPAD_RIGHT_THUMB      0x0080
//#define XINPUT_GAMEPAD_LEFT_SHOULDER    0x0100
//#define XINPUT_GAMEPAD_RIGHT_SHOULDER   0x0200
//#define XINPUT_GAMEPAD_A                0x1000
//#define XINPUT_GAMEPAD_B                0x2000
//#define XINPUT_GAMEPAD_X                0x4000
//#define XINPUT_GAMEPAD_Y                0x8000
    public class VGenWrapper
    {
        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus isVBusExist();

        public bool vbox_isVBusExist()
        {
            var result = isVBusExist();
            return result == NtStatus.Success;
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus GetNumEmptyBusSlots([Out] uint[] nSlots);

        public uint vbox_GetNumEmptyBusSlots()
        {
            var nslots = new uint[1];
            var result = GetNumEmptyBusSlots(nslots);

            return nslots[0];
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus PlugIn(uint UserIndex);

        public bool vbox_PlugIn(uint id)
        {
            var returnValue = PlugIn(id);
            return returnValue != 0;
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus UnPlug(uint UserIndex);

        public bool vbox_UnPlug(uint id)
        {
            return UnPlug(id) != 0;
        }


        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetButton(uint UserIndex, int button, bool pressed);
        public bool vbox_SetBtn(uint id, int button, bool pressed)
        {
            return SetButton(id, button, pressed);
        }
    }
}