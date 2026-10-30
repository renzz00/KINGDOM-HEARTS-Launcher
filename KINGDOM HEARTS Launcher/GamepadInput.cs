using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Kingdom_Hearts_Launcher
{
    public enum PadAction
    {
        Up,
        Down,
        Confirm,
        Config,
        Cancel
    }

    // Polls XInput pads and DirectInput-style pads (through the winmm joystick API, which sees them too).
    // Left stick / D-pad moves the selection, A (Cross) confirms, X (Square) opens the config, B (Circle) cancels.
    public sealed class GamepadInput : IDisposable
    {
        private const ushort XInputDpadUp = 0x0001;
        private const ushort XInputDpadDown = 0x0002;
        private const short XInputStickThreshold = 16000;

        private const int WinmmCenter = 32767;
        private const int WinmmThreshold = 16000;

        // Face buttons in the order of ButtonActions. In DirectInput mode, buttons 1-3 are X/A/B on Xbox-layout
        // pads and Square/Cross/Circle on PlayStation pads, so the same masks serve both.
        private static readonly PadAction[] ButtonActions = { PadAction.Confirm, PadAction.Config, PadAction.Cancel };
        private static readonly ushort[] XInputMasks = { 0x1000 /* A */, 0x4000 /* X */, 0x2000 /* B */ };
        private static readonly uint[] WinmmMasks = { 0x2, 0x1, 0x4 };

        private static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(400);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(120);

        private readonly DispatcherTimer timer;
        private readonly List<uint> xinputSlots = new List<uint>();
        private readonly List<uint> winmmIds = new List<uint>();
        private DateTime nextRescan = DateTime.MinValue;
        private bool xinputUsable = true;
        private bool xinputLegacyDll;

        private int heldDirection;
        private DateTime nextRepeat;
        private readonly bool[] buttonsHeld = new bool[ButtonActions.Length];

        // Input is only raised while this returns true (e.g. one of our windows is active), so a pad press can't act behind a message box.
        public Func<bool> CanRaise { get; set; } = () => true;
        public event Action<PadAction> ActionRaised;

        public GamepadInput()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
            timer.Tick += (s, e) => Poll();
        }

        public bool Enabled
        {
            get { return timer.IsEnabled; }
            set
            {
                if (value == timer.IsEnabled) return;
                if (value)
                {
                    nextRescan = DateTime.MinValue;
                    timer.Start();
                }
                else
                {
                    timer.Stop();
                }
            }
        }

        public void Dispose()
        {
            timer.Stop();
        }

        private void Poll()
        {
            DateTime now = DateTime.UtcNow;
            if (now >= nextRescan)
            {
                Rescan();
                nextRescan = now + RescanInterval;
            }

            bool up = false, down = false;
            bool[] pressed = new bool[ButtonActions.Length];
            bool xinputConnected = ReadXInput(ref up, ref down, pressed);
            ReadWinmm(ref up, ref down, pressed, ignoreButtons: xinputConnected);

            int direction = up == down ? 0 : (down ? 1 : -1);
            bool canRaise = CanRaise();

            if (direction != heldDirection)
            {
                heldDirection = direction;
                nextRepeat = now + RepeatDelay;
                if (direction != 0 && canRaise)
                {
                    Raise(direction > 0 ? PadAction.Down : PadAction.Up);
                }
            }
            else if (direction != 0 && now >= nextRepeat)
            {
                nextRepeat = now + RepeatRate;
                if (canRaise)
                {
                    Raise(direction > 0 ? PadAction.Down : PadAction.Up);
                }
            }

            for (int i = 0; i < pressed.Length; i++)
            {
                if (pressed[i] && !buttonsHeld[i] && canRaise)
                {
                    Raise(ButtonActions[i]);
                }
                buttonsHeld[i] = pressed[i];
            }
        }

        // Handlers can block in a nested message loop (e.g. ShowDialog); running them inside the timer tick would
        // stop the timer, and with it all pad input, until they return.
        private void Raise(PadAction action)
        {
            timer.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ActionRaised?.Invoke(action)));
        }

        // Polling a disconnected slot is slow on some systems, so only connected devices are polled every tick.
        private void Rescan()
        {
            xinputSlots.Clear();
            if (xinputUsable)
            {
                for (uint i = 0; i < 4; i++)
                {
                    NativeMethods.XINPUT_STATE state;
                    if (GetXInputState(i, out state) == 0)
                    {
                        xinputSlots.Add(i);
                    }
                }
            }

            winmmIds.Clear();
            uint count = NativeMethods.joyGetNumDevs();
            for (uint id = 0; id < count; id++)
            {
                NativeMethods.JOYINFOEX info = NewJoyInfo();
                if (NativeMethods.joyGetPosEx(id, ref info) == 0)
                {
                    winmmIds.Add(id);
                }
            }
        }

        private uint GetXInputState(uint slot, out NativeMethods.XINPUT_STATE state)
        {
            try
            {
                return xinputLegacyDll
                    ? NativeMethods.XInputGetStateLegacy(slot, out state)
                    : NativeMethods.XInputGetState(slot, out state);
            }
            catch (DllNotFoundException)
            {
                if (!xinputLegacyDll)
                {
                    xinputLegacyDll = true;
                    return GetXInputState(slot, out state);
                }
                xinputUsable = false;
            }
            catch (EntryPointNotFoundException)
            {
                xinputUsable = false;
            }

            state = default(NativeMethods.XINPUT_STATE);
            return 1167; // ERROR_DEVICE_NOT_CONNECTED
        }

        private bool ReadXInput(ref bool up, ref bool down, bool[] pressed)
        {
            bool connected = false;
            foreach (uint slot in xinputSlots)
            {
                NativeMethods.XINPUT_STATE state;
                if (GetXInputState(slot, out state) != 0)
                {
                    continue;
                }

                connected = true;
                NativeMethods.XINPUT_GAMEPAD pad = state.Gamepad;
                up |= (pad.wButtons & XInputDpadUp) != 0 || pad.sThumbLY > XInputStickThreshold;
                down |= (pad.wButtons & XInputDpadDown) != 0 || pad.sThumbLY < -XInputStickThreshold;
                for (int i = 0; i < pressed.Length; i++)
                {
                    pressed[i] |= (pad.wButtons & XInputMasks[i]) != 0;
                }
            }
            return connected;
        }

        // XInput pads also show up in winmm; their buttons are ignored there so the XInput path alone handles them.
        private void ReadWinmm(ref bool up, ref bool down, bool[] pressed, bool ignoreButtons)
        {
            foreach (uint id in winmmIds)
            {
                NativeMethods.JOYINFOEX info = NewJoyInfo();
                if (NativeMethods.joyGetPosEx(id, ref info) != 0)
                {
                    continue;
                }

                up |= info.dwYpos < WinmmCenter - WinmmThreshold || PovPointsUp(info.dwPOV);
                down |= info.dwYpos > WinmmCenter + WinmmThreshold || PovPointsDown(info.dwPOV);
                if (!ignoreButtons)
                {
                    for (int i = 0; i < pressed.Length; i++)
                    {
                        pressed[i] |= (info.dwButtons & WinmmMasks[i]) != 0;
                    }
                }
            }
        }

        private static bool PovPointsUp(uint pov)
        {
            return pov <= 36000 && (pov <= 4500 || pov >= 31500);
        }

        private static bool PovPointsDown(uint pov)
        {
            return pov >= 13500 && pov <= 22500;
        }

        private static NativeMethods.JOYINFOEX NewJoyInfo()
        {
            const uint JOY_RETURNALL = 0x000000FF;
            return new NativeMethods.JOYINFOEX
            {
                dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.JOYINFOEX)),
                dwFlags = JOY_RETURNALL
            };
        }

        private static class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct XINPUT_GAMEPAD
            {
                public ushort wButtons;
                public byte bLeftTrigger;
                public byte bRightTrigger;
                public short sThumbLX;
                public short sThumbLY;
                public short sThumbRX;
                public short sThumbRY;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct XINPUT_STATE
            {
                public uint dwPacketNumber;
                public XINPUT_GAMEPAD Gamepad;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct JOYINFOEX
            {
                public uint dwSize;
                public uint dwFlags;
                public uint dwXpos;
                public uint dwYpos;
                public uint dwZpos;
                public uint dwRpos;
                public uint dwUpos;
                public uint dwVpos;
                public uint dwButtons;
                public uint dwButtonNumber;
                public uint dwPOV;
                public uint dwReserved1;
                public uint dwReserved2;
            }

            [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
            public static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);

            [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
            public static extern uint XInputGetStateLegacy(uint dwUserIndex, out XINPUT_STATE pState);

            [DllImport("winmm.dll")]
            public static extern uint joyGetNumDevs();

            [DllImport("winmm.dll")]
            public static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);
        }
    }
}
