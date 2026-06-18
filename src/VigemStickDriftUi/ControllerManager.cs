using System;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace VigemStickDriftUi
{
    public class ControllerManager
    {
        private ViGEmClient _client;
        private IXbox360Controller _xboxController;
        private IDualShock4Controller _ds4Controller;

        public bool IsConnected { get; private set; }
        public bool UseXbox { get; private set; }

        public void Connect(string controllerType)
        {
            Disconnect();

            _client = new ViGEmClient();
            UseXbox = string.Equals(controllerType, "Xbox 360", StringComparison.OrdinalIgnoreCase);

            if (UseXbox)
            {
                _xboxController = _client.CreateXbox360Controller();
                _xboxController.Connect();
            }
            else
            {
                _ds4Controller = _client.CreateDualShock4Controller();
                _ds4Controller.Connect();
            }

            IsConnected = true;
        }

        public void Disconnect()
        {
            try { _xboxController?.Disconnect(); } catch { }
            try { _ds4Controller?.Disconnect(); } catch { }

            _xboxController = null;
            _ds4Controller = null;
            _client = null;
            IsConnected = false;
        }

        public void SetStick(int percentX, int percentY)
        {
            if (!IsConnected) return;

            if (UseXbox && _xboxController != null)
            {
                _xboxController.SetAxisValue(Xbox360Axis.RightThumbX, PercentToXboxAxis(percentX));
                _xboxController.SetAxisValue(Xbox360Axis.RightThumbY, PercentToXboxAxis(percentY));
            }
            else if (!UseXbox && _ds4Controller != null)
            {
                _ds4Controller.SetAxisValue(DualShock4Axis.RightThumbX, PercentToDs4Axis(percentX));
                _ds4Controller.SetAxisValue(DualShock4Axis.RightThumbY, PercentToDs4Axis(percentY));
            }
        }

        public void ApplyBindings(Func<string, bool> isPressedProvider)
        {
            if (!IsConnected) return;

            if (UseXbox && _xboxController != null)
            {
                _xboxController.SetButtonState(Xbox360Button.A, isPressedProvider("A / Cross"));
                _xboxController.SetButtonState(Xbox360Button.B, isPressedProvider("B / Circle"));
                _xboxController.SetButtonState(Xbox360Button.X, isPressedProvider("X / Square"));
                _xboxController.SetButtonState(Xbox360Button.Y, isPressedProvider("Y / Triangle"));
                _xboxController.SetButtonState(Xbox360Button.LeftShoulder, isPressedProvider("LB / L1"));
                _xboxController.SetButtonState(Xbox360Button.RightShoulder, isPressedProvider("RB / R1"));
                _xboxController.SetButtonState(Xbox360Button.Back, isPressedProvider("Back / Share"));
                _xboxController.SetButtonState(Xbox360Button.Start, isPressedProvider("Start / Options"));
                _xboxController.SetButtonState(Xbox360Button.LeftThumb, isPressedProvider("LS / L3"));
                _xboxController.SetButtonState(Xbox360Button.RightThumb, isPressedProvider("RS / R3"));

                _xboxController.SetSliderValue(Xbox360Slider.LeftTrigger, isPressedProvider("LT / L2") ? byte.MaxValue : (byte)0);
                _xboxController.SetSliderValue(Xbox360Slider.RightTrigger, isPressedProvider("RT / R2") ? byte.MaxValue : (byte)0);

                _xboxController.SetButtonState(Xbox360Button.Up, isPressedProvider("DPad Up"));
                _xboxController.SetButtonState(Xbox360Button.Down, isPressedProvider("DPad Down"));
                _xboxController.SetButtonState(Xbox360Button.Left, isPressedProvider("DPad Left"));
                _xboxController.SetButtonState(Xbox360Button.Right, isPressedProvider("DPad Right"));
            }
            else if (!UseXbox && _ds4Controller != null)
            {
                _ds4Controller.SetButtonState(DualShock4Button.Cross, isPressedProvider("A / Cross"));
                _ds4Controller.SetButtonState(DualShock4Button.Circle, isPressedProvider("B / Circle"));
                _ds4Controller.SetButtonState(DualShock4Button.Square, isPressedProvider("X / Square"));
                _ds4Controller.SetButtonState(DualShock4Button.Triangle, isPressedProvider("Y / Triangle"));
                _ds4Controller.SetButtonState(DualShock4Button.ShoulderLeft, isPressedProvider("LB / L1"));
                _ds4Controller.SetButtonState(DualShock4Button.ShoulderRight, isPressedProvider("RB / R1"));
                _ds4Controller.SetButtonState(DualShock4Button.TriggerLeft, isPressedProvider("LT / L2"));
                _ds4Controller.SetButtonState(DualShock4Button.TriggerRight, isPressedProvider("RT / R2"));
                _ds4Controller.SetButtonState(DualShock4Button.Share, isPressedProvider("Back / Share"));
                _ds4Controller.SetButtonState(DualShock4Button.Options, isPressedProvider("Start / Options"));
                _ds4Controller.SetButtonState(DualShock4Button.ThumbLeft, isPressedProvider("LS / L3"));
                _ds4Controller.SetButtonState(DualShock4Button.ThumbRight, isPressedProvider("RS / R3"));

                _ds4Controller.SetSliderValue(DualShock4Slider.LeftTrigger, isPressedProvider("LT / L2") ? byte.MaxValue : (byte)0);
                _ds4Controller.SetSliderValue(DualShock4Slider.RightTrigger, isPressedProvider("RT / R2") ? byte.MaxValue : (byte)0);
                _ds4Controller.SetDPadDirection(GetDs4DPadDirection(isPressedProvider));
            }
        }

        public void SubmitReport()
        {
            if (!IsConnected) return;
            if (UseXbox) _xboxController?.SubmitReport();
            else _ds4Controller?.SubmitReport();
        }

        public void SendNeutralStick()
        {
            SetStick(50, 50);
            SubmitReport();
        }

        public string GetConnectedControllerName() => UseXbox ? "Xbox 360" : "PS4";

        private static short PercentToXboxAxis(int percent)
        {
            double ratio = Math.Clamp(percent, 0, 100) / 100.0;
            return (short)Math.Round(short.MinValue + ratio * (short.MaxValue - short.MinValue));
        }

        private static byte PercentToDs4Axis(int percent)
        {
            double ratio = Math.Clamp(percent, 0, 100) / 100.0;
            return (byte)Math.Round(ratio * byte.MaxValue);
        }

        private static DualShock4DPadDirection GetDs4DPadDirection(Func<string, bool> isPressedProvider)
        {
            bool up = isPressedProvider("DPad Up");
            bool down = isPressedProvider("DPad Down");
            bool left = isPressedProvider("DPad Left");
            bool right = isPressedProvider("DPad Right");

            if (up && right && !down && !left) return DualShock4DPadDirection.Northeast;
            if (up && left && !down && !right) return DualShock4DPadDirection.Northwest;
            if (down && right && !up && !left) return DualShock4DPadDirection.Southeast;
            if (down && left && !up && !right) return DualShock4DPadDirection.Southwest;
            if (up && !down && !left && !right) return DualShock4DPadDirection.North;
            if (down && !up && !left && !right) return DualShock4DPadDirection.South;
            if (left && !up && !down && !right) return DualShock4DPadDirection.West;
            if (right && !up && !down && !left) return DualShock4DPadDirection.East;
            return DualShock4DPadDirection.None;
        }
    }
}