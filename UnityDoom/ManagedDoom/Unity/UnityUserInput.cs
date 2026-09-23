using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ManagedDoom.UserInput;

namespace ManagedDoom.Unity
{
    public sealed class UnityUserInput : IUserInput, IDisposable
    {
        public UnityContext unityContext;

        public Config config;

        private bool useMouse;

        private bool[] weaponKeys;
        private int turnHeld;

        private bool mouseGrabbed;
        private int windowCenterX;
        private int windowCenterY;
        private int mouseX;
        private int mouseY;
        private Queue<DoomEvent> inputEvents;

        // Big Walk's interop assemblies don't include the new Input System
        // package (no UnityEngine.InputSystem.dll present) - this uses the
        // legacy Input Manager (UnityEngine.Input / KeyCode) instead.
        private static readonly Dictionary<DoomKey, KeyCode> keyMapping = new Dictionary<DoomKey, KeyCode>()
        {
            { DoomKey.Unknown, KeyCode.None },
            { DoomKey.A, KeyCode.A },
            { DoomKey.B, KeyCode.B },
            { DoomKey.C, KeyCode.C },
            { DoomKey.D, KeyCode.D },
            { DoomKey.E, KeyCode.E },
            { DoomKey.F, KeyCode.F },
            { DoomKey.G, KeyCode.G },
            { DoomKey.H, KeyCode.H },
            { DoomKey.I, KeyCode.I },
            { DoomKey.J, KeyCode.J },
            { DoomKey.K, KeyCode.K },
            { DoomKey.L, KeyCode.L },
            { DoomKey.M, KeyCode.M },
            { DoomKey.N, KeyCode.N },
            { DoomKey.O, KeyCode.O },
            { DoomKey.P, KeyCode.P },
            { DoomKey.Q, KeyCode.Q },
            { DoomKey.R, KeyCode.R },
            { DoomKey.S, KeyCode.S },
            { DoomKey.T, KeyCode.T },
            { DoomKey.U, KeyCode.U },
            { DoomKey.V, KeyCode.V },
            { DoomKey.W, KeyCode.W },
            { DoomKey.X, KeyCode.X },
            { DoomKey.Y, KeyCode.Y },
            { DoomKey.Z, KeyCode.Z },
            { DoomKey.Num0, KeyCode.Alpha0 },
            { DoomKey.Num1, KeyCode.Alpha1 },
            { DoomKey.Num2, KeyCode.Alpha2 },
            { DoomKey.Num3, KeyCode.Alpha3 },
            { DoomKey.Num4, KeyCode.Alpha4 },
            { DoomKey.Num5, KeyCode.Alpha5 },
            { DoomKey.Num6, KeyCode.Alpha6 },
            { DoomKey.Num7, KeyCode.Alpha7 },
            { DoomKey.Num8, KeyCode.Alpha8 },
            { DoomKey.Num9, KeyCode.Alpha9 },
            { DoomKey.Escape, KeyCode.Escape },
            { DoomKey.LControl, KeyCode.LeftControl },
            { DoomKey.LShift, KeyCode.LeftShift },
            { DoomKey.LAlt, KeyCode.LeftAlt },
            { DoomKey.LSystem, KeyCode.LeftWindows },
            { DoomKey.RControl, KeyCode.RightControl },
            { DoomKey.RShift, KeyCode.RightShift },
            { DoomKey.RAlt, KeyCode.RightAlt },
            { DoomKey.RSystem, KeyCode.RightWindows },
            { DoomKey.Menu, KeyCode.Menu },
            { DoomKey.LBracket, KeyCode.LeftBracket },
            { DoomKey.RBracket, KeyCode.RightBracket },
            { DoomKey.Semicolon, KeyCode.Semicolon },
            { DoomKey.Comma, KeyCode.Comma },
            { DoomKey.Period, KeyCode.Period },
            { DoomKey.Quote, KeyCode.Quote },
            { DoomKey.Slash, KeyCode.Slash },
            { DoomKey.Backslash, KeyCode.Backslash },
            { DoomKey.Tilde, KeyCode.BackQuote },
            { DoomKey.Equal, KeyCode.Equals },
            { DoomKey.Hyphen, KeyCode.Minus },
            { DoomKey.Space, KeyCode.Space },
            { DoomKey.Enter, KeyCode.Return },
            { DoomKey.Backspace, KeyCode.Backspace },
            { DoomKey.Tab, KeyCode.Tab },
            { DoomKey.PageUp, KeyCode.PageUp },
            { DoomKey.PageDown, KeyCode.PageDown },
            { DoomKey.End, KeyCode.End },
            { DoomKey.Home, KeyCode.Home },
            { DoomKey.Insert, KeyCode.Insert },
            { DoomKey.Delete, KeyCode.Delete },
            { DoomKey.Subtract, KeyCode.Minus },
            { DoomKey.Divide, KeyCode.Slash },
            { DoomKey.Left, KeyCode.LeftArrow },
            { DoomKey.Right, KeyCode.RightArrow },
            { DoomKey.Up, KeyCode.UpArrow },
            { DoomKey.Down, KeyCode.DownArrow },
            { DoomKey.Numpad0, KeyCode.Keypad0 },
            { DoomKey.Numpad1, KeyCode.Keypad1 },
            { DoomKey.Numpad2, KeyCode.Keypad2 },
            { DoomKey.Numpad3, KeyCode.Keypad3 },
            { DoomKey.Numpad4, KeyCode.Keypad4 },
            { DoomKey.Numpad5, KeyCode.Keypad5 },
            { DoomKey.Numpad6, KeyCode.Keypad6 },
            { DoomKey.Numpad7, KeyCode.Keypad7 },
            { DoomKey.Numpad8, KeyCode.Keypad8 },
            { DoomKey.Numpad9, KeyCode.Keypad9 },
            { DoomKey.F1, KeyCode.F1 },
            { DoomKey.F2, KeyCode.F2 },
            { DoomKey.F3, KeyCode.F3 },
            { DoomKey.F4, KeyCode.F4 },
            { DoomKey.F5, KeyCode.F5 },
            { DoomKey.F6, KeyCode.F6 },
            { DoomKey.F7, KeyCode.F7 },
            { DoomKey.F8, KeyCode.F8 },
            { DoomKey.F9, KeyCode.F9 },
            { DoomKey.F10, KeyCode.F10 },
            { DoomKey.F11, KeyCode.F11 },
            { DoomKey.F12, KeyCode.F12 },
            { DoomKey.Pause, KeyCode.Pause },
        };

        public UnityUserInput(Config config, bool useMouse, UnityContext unityContext)
        {
            try
            {
                Logger.Log("Initialize user input: ");

                this.config = config;
                this.unityContext = unityContext;

                config.mouse_sensitivity = Math.Max(config.mouse_sensitivity, 0);

                this.useMouse = useMouse;

                weaponKeys = new bool[7];
                turnHeld = 0;

                mouseGrabbed = false;
                windowCenterX = (int)Screen.width / 2;
                windowCenterY = (int)Screen.height / 2;
                mouseX = 0;
                mouseY = 0;

                inputEvents = new Queue<DoomEvent>();

                Logger.Log("OK");
            }
            catch
            {
                Logger.Log("Failed");
                Dispose();
                throw;
            }
        }

        public Queue<DoomEvent> GenerateEvents()
        {
            inputEvents.Clear();
            if (!unityContext.AllowInput) return inputEvents;
            foreach (var pair in keyMapping)
            {
                if (pair.Key == DoomKey.Unknown) continue;
                if (Input.GetKeyDown(pair.Value))
                {
                    inputEvents.Enqueue(new DoomEvent(EventType.KeyDown, pair.Key));
                }
                if (Input.GetKeyUp(pair.Value))
                {
                    inputEvents.Enqueue(new DoomEvent(EventType.KeyUp, pair.Key));
                }
            }
            return inputEvents;
        }

        public void BuildTicCmd(TicCmd cmd)
        {
            var keyForward = IsPressed(config.key_forward);
            var keyBackward = IsPressed(config.key_backward);
            var keyStrafeLeft = IsPressed(config.key_strafeleft);
            var keyStrafeRight = IsPressed(config.key_straferight);
            var keyTurnLeft = IsPressed(config.key_turnleft);
            var keyTurnRight = IsPressed(config.key_turnright);
            var keyFire = IsPressed(config.key_fire);
            var keyUse = IsPressed(config.key_use);
            var keyRun = IsPressed(config.key_run);
            var keyStrafe = IsPressed(config.key_strafe);

            weaponKeys[0] = Input.GetKey(KeyCode.Alpha1);
            weaponKeys[1] = Input.GetKey(KeyCode.Alpha2);
            weaponKeys[2] = Input.GetKey(KeyCode.Alpha3);
            weaponKeys[3] = Input.GetKey(KeyCode.Alpha4);
            weaponKeys[4] = Input.GetKey(KeyCode.Alpha5);
            weaponKeys[5] = Input.GetKey(KeyCode.Alpha6);
            weaponKeys[6] = Input.GetKey(KeyCode.Alpha7);

            cmd.Clear();

            var strafe = keyStrafe;
            var speed = keyRun ? 1 : 0;
            var forward = 0;
            var side = 0;

            if (config.game_alwaysrun)
            {
                speed = 1 - speed;
            }

            if (keyTurnLeft || keyTurnRight)
            {
                turnHeld++;
            }
            else
            {
                turnHeld = 0;
            }

            int turnSpeed;
            if (turnHeld < PlayerBehavior.SlowTurnTics)
            {
                turnSpeed = 2;
            }
            else
            {
                turnSpeed = speed;
            }

            if (strafe)
            {
                if (keyTurnRight)
                {
                    side += PlayerBehavior.SideMove[speed];
                }
                if (keyTurnLeft)
                {
                    side -= PlayerBehavior.SideMove[speed];
                }
            }
            else
            {
                if (keyTurnRight)
                {
                    cmd.AngleTurn -= (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
                if (keyTurnLeft)
                {
                    cmd.AngleTurn += (short)PlayerBehavior.AngleTurn[turnSpeed];
                }
            }

            if (keyForward)
            {
                forward += PlayerBehavior.ForwardMove[speed];
            }
            if (keyBackward)
            {
                forward -= PlayerBehavior.ForwardMove[speed];
            }

            if (keyStrafeLeft)
            {
                side -= PlayerBehavior.SideMove[speed];
            }
            if (keyStrafeRight)
            {
                side += PlayerBehavior.SideMove[speed];
            }

            if (keyFire)
            {
                cmd.Buttons |= TicCmdButtons.Attack;
            }

            if (keyUse)
            {
                cmd.Buttons |= TicCmdButtons.Use;
            }

            // Check weapon keys.
            for (var i = 0; i < weaponKeys.Length; i++)
            {
                if (weaponKeys[i])
                {
                    cmd.Buttons |= TicCmdButtons.Change;
                    cmd.Buttons |= (byte)(i << TicCmdButtons.WeaponShift);
                    break;
                }
            }

            UpdateMouse();
            var ms = 0.5F * config.mouse_sensitivity;
            var mx = (int)Mathf.Round(ms * mouseX);
            var my = (int)Mathf.Round(ms * mouseY);
            forward += my;
            if (strafe)
            {
                side += mx * 2;
            }
            else
            {
                cmd.AngleTurn -= (short)(mx * 0x8);
            }

            if (forward > PlayerBehavior.MaxMove)
            {
                forward = PlayerBehavior.MaxMove;
            }
            else if (forward < -PlayerBehavior.MaxMove)
            {
                forward = -PlayerBehavior.MaxMove;
            }
            if (side > PlayerBehavior.MaxMove)
            {
                side = PlayerBehavior.MaxMove;
            }
            else if (side < -PlayerBehavior.MaxMove)
            {
                side = -PlayerBehavior.MaxMove;
            }

            cmd.ForwardMove += (sbyte)forward;
            cmd.SideMove += (sbyte)side;
        }

        private bool IsPressed(KeyBinding keyBinding)
        {
            if (!unityContext.AllowInput) return false;
            foreach (var key in keyBinding.Keys)
            {
                if (IsKeyPressed(key))
                {
                    return true;
                }
            }

            if (mouseGrabbed)
            {
                foreach (var mouseButton in keyBinding.MouseButtons)
                {
                    // Legacy Input maps mouse buttons as: 0=left, 1=right,
                    // 2=middle, 3=back (Mouse4/XButton1), 4=forward (Mouse5/XButton2).
                    if (mouseButton == DoomMouseButton.Mouse1 && Input.GetMouseButton(0)) return true;
                    if (mouseButton == DoomMouseButton.Mouse2 && Input.GetMouseButton(1)) return true;
                    if (mouseButton == DoomMouseButton.Mouse3 && Input.GetMouseButton(2)) return true;
                    if (mouseButton == DoomMouseButton.Mouse4 && Input.GetMouseButton(3)) return true;
                    if (mouseButton == DoomMouseButton.Mouse5 && Input.GetMouseButton(4)) return true;
                }
            }

            return false;
        }

        private bool IsKeyPressed(DoomKey key)
        {
            if (!unityContext.AllowInput) return false;
            return Input.GetKey(keyMapping[key]);
        }

        public void Reset()
        {
            mouseX = 0;
            mouseY = 0;
        }

        public void GrabMouse()
        {
            if (useMouse && !mouseGrabbed)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                mouseGrabbed = true;
                mouseX = 0;
                mouseY = 0;
            }
        }

        public void ReleaseMouse()
        {
            if (useMouse && mouseGrabbed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                mouseGrabbed = false;
            }
        }

        private void UpdateMouse()
        {
            if (mouseGrabbed)
            {
                // Legacy Input's "Mouse X"/"Mouse Y" axes report per-frame
                // delta directly while the cursor is locked - no need to
                // manually diff against a remembered window-center position
                // the way the new Input System's raw position API required.
                mouseX = (int)Input.GetAxisRaw("Mouse X");
                mouseY = config.mouse_disableyaxis ? 0 : (int)-Input.GetAxisRaw("Mouse Y");
            }
            else
            {
                mouseX = 0;
                mouseY = 0;
            }
        }

        public void Dispose()
        {
            Logger.Log("Shutdown user input.");

            ReleaseMouse();
        }

        public int MaxMouseSensitivity
        {
            get
            {
                return 15;
            }
        }

        public int MouseSensitivity
        {
            get
            {
                return config.mouse_sensitivity;
            }

            set
            {
                config.mouse_sensitivity = value;
            }
        }
    }
}