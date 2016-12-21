using System;
using System.Runtime.InteropServices;

namespace vGenWrapper
{
    public class VGenWrapper
    {
        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus isVBusExist();

        public NtStatus vbox_isVBusExist()
        {
            return isVBusExist();
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus PlugIn(uint userIndex);

        public NtStatus vbox_PlugIn(uint slotId)
        {
            return PlugIn(slotId);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus UnPlug(uint userIndex);

        public NtStatus vbox_UnPlug(uint slotId)
        {
            return UnPlug(slotId);
        }


        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus UnPlugForce(uint userIndex);

        public NtStatus vbox_ForceUnPlug(uint slotId)
        {
            return UnPlugForce(slotId);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus isControllerPluggedIn(uint userIndex, ref bool exists);

        public NtStatus vbox_isControllerPluggedIn(uint slotId, ref bool exists)
        {
            return isControllerPluggedIn(slotId, ref exists);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetButton(uint userIndex, ushort button, bool pressed);

        public NtStatus vbox_SetButton(uint slotId, XInputGamepadButtons button, bool pressed)
        {
            return SetButton(slotId, (ushort) button, pressed);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetAxisLx(uint userIndex, short value);

        public NtStatus vbox_SetAxisLx(uint slotId, short value)
        {
            return SetAxisLx(slotId, value);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetAxisLy(uint userIndex, short value);

        public NtStatus vbox_SetAxisLy(uint slotId, short value)
        {
            return SetAxisLy(slotId, value);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetAxisRx(uint userIndex, short value);

        public NtStatus vbox_SetAxisRx(uint slotId, short value)
        {
            return SetAxisRx(slotId, value);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetAxisRy(uint userIndex, short value);

        public NtStatus vbox_SetAxisRy(uint slotId, short value)
        {
            return SetAxisRy(slotId, value);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetTriggerL(uint userIndex, byte value);

        public NtStatus vbox_SetTriggerL(uint slotId, byte value)
        {
            return SetTriggerL(slotId, value);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetTriggerR(uint userIndex, byte value);

        public NtStatus vbox_SetTriggerR(uint slotId, byte value)
        {
            return SetTriggerR(slotId, value);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus SetDpad(uint userIndex, ushort value);

        public NtStatus vbox_SetDpad(uint slotId, XInputGamepadButtons buttons)
        {
            return SetDpad(slotId, (ushort) buttons);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus ResetController(uint userIndex);

        public NtStatus vbox_ResetController(uint slotId)
        {
            return ResetController(slotId);
        }

        [DllImport("vGenInterface.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NtStatus ResetAllControllers();

        public NtStatus vbox_ResetAllControllers()
        {
            return ResetAllControllers();
        }

    }
}