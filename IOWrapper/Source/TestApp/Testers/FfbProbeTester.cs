using System;
using vJoyInterfaceWrap;
using static vJoyInterfaceWrap.vJoy;

namespace TestApp.Testers
{
    /// <summary>
    /// Throwaway hardware probe for the vJoy FFB passthrough plan - NOT part of the shipped feature.
    /// Confirms, against a real vJoy driver, whether FfbRegisterGenCB is a single process-wide
    /// callback (device ID read per-packet via Ffb_h_DeviceID) as the plan assumes, or something else.
    ///
    /// Usage: uncomment the line that constructs this in Launcher.cs, run TestApp.exe, then:
    ///   1. Make sure at least one vJoy device is configured (vJoyConf) with an axis and is a
    ///      valid Windows game controller.
    ///   2. Control Panel -> Devices and Printers -> right-click the vJoy device ->
    ///      Game controller settings -> Properties -> Test tab -> trigger any force feedback effect.
    ///   3. Watch the console for "FFB PACKET" lines.
    /// If only one callback ever fires regardless of which vJoy device you test, and DeviceId in the
    /// printed line correctly identifies which one you triggered, the plan's assumption is confirmed.
    /// </summary>
    public class FfbProbeTester
    {
        private readonly vJoy _vj = new vJoy();

        public FfbProbeTester()
        {
            if (!_vj.vJoyEnabled())
            {
                Console.WriteLine("FFB PROBE: vJoyEnabled() returned false - vJoy driver not installed/running. Aborting probe.");
                return;
            }

            _vj.FfbRegisterGenCB(OnFfbPacket, null);
            Console.WriteLine("FFB PROBE: FfbRegisterGenCB registered (one process-wide callback, no device ID passed at registration).");
            Console.WriteLine("FFB PROBE: Now trigger a Force Feedback effect on a vJoy device (Devices and Printers -> Game controller settings -> Properties -> Test tab) and watch for 'FFB PACKET' lines below.");
        }

        private void OnFfbPacket(IntPtr data, object userData)
        {
            var deviceId = 0;
            var type = default(FFBPType);
            _vj.Ffb_h_DeviceID(data, ref deviceId);
            _vj.Ffb_h_Type(data, ref type);

            var detail = string.Empty;
            switch (type)
            {
                case FFBPType.PT_EFOPREP:
                    var effOp = default(FFB_EFF_OP);
                    _vj.Ffb_h_EffOp(data, ref effOp);
                    detail = $"EffectBlockIndex={effOp.EffectBlockIndex}, Op={effOp.EffectOp}";
                    break;
                case FFBPType.PT_CONSTREP:
                    var constant = default(FFB_EFF_CONSTANT);
                    _vj.Ffb_h_Eff_Constant(data, ref constant);
                    detail = $"EffectBlockIndex={constant.EffectBlockIndex}, Magnitude={constant.Magnitude}";
                    break;
                case FFBPType.PT_CTRLREP:
                    var ctrl = default(FFB_CTRL);
                    _vj.Ffb_h_DevCtrl(data, ref ctrl);
                    detail = $"Control={ctrl}";
                    break;
                case FFBPType.PT_GAINREP:
                    byte gain = 0;
                    _vj.Ffb_h_DevGain(data, ref gain);
                    detail = $"Gain={gain}";
                    break;
                case FFBPType.PT_NEWEFREP:
                    var effType = default(FFBEType);
                    _vj.Ffb_h_EffNew(data, ref effType);
                    detail = $"NewEffectType={effType}";
                    break;
            }

            Console.WriteLine($"FFB PACKET: DeviceId={deviceId}, Type={type}{(detail.Length > 0 ? ", " + detail : string.Empty)}");
        }
    }
}
