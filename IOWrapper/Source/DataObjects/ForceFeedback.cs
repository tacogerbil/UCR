using System;

namespace HidWizards.IOWrapper.DataTransferObjects
{
    /// <summary>
    /// The lifecycle operation a Force Feedback command represents
    /// </summary>
    public enum FfbOperation
    {
        Create,
        Update,
        Start,
        Stop,
        Destroy,
        SetDeviceGain,
        SetAutocenter
    }

    /// <summary>
    /// The DirectInput effect shape a Force Feedback command carries
    /// Unused fields on <see cref="FfbCommand"/> for a given type are left at their default
    /// </summary>
    public enum FfbEffectType
    {
        Constant,
        Ramp,
        Square,
        Sine,
        Triangle,
        SawtoothUp,
        SawtoothDown,
        Spring,
        Damper,
        Friction,
        Inertia
    }

    /// <summary>
    /// A single decoded Force Feedback command, provider-agnostic
    /// Raised by an <see cref="ProviderInterface.Interfaces.IFfbSourceProvider"/> (eg vJoy, decoding a game's
    /// DirectInput FFB call) and consumed by an <see cref="ProviderInterface.Interfaces.IFfbSinkProvider"/>
    /// (eg SharpDX_DirectInput, replaying it on a real wheel)
    /// </summary>
    public class FfbCommand
    {
        /// <summary>
        /// The source device's own effect block index. One physical/virtual device can have several
        /// effects live at once (eg a Constant force stacked with a Spring) - this is what lets a sink
        /// tell them apart for Update/Start/Stop/Destroy
        /// </summary>
        public int EffectId { get; set; }

        public FfbOperation Operation { get; set; }

        public FfbEffectType EffectType { get; set; }

        /// <summary>
        /// Force magnitude, -10000 to 10000 (DirectInput's own range)
        /// </summary>
        public int Magnitude { get; set; }

        /// <summary>
        /// Direction of the effect, in hundredths of a degree (DirectInput polar convention)
        /// </summary>
        public int Direction { get; set; }

        /// <summary>
        /// Duration in milliseconds. int.MaxValue means infinite (DirectInput's INFINITE)
        /// </summary>
        public int DurationMs { get; set; }

        // Condition effect (Spring / Damper / Friction / Inertia) parameters
        public int ConditionOffset { get; set; }
        public int ConditionPositiveCoefficient { get; set; }
        public int ConditionNegativeCoefficient { get; set; }
        public int ConditionPositiveSaturation { get; set; }
        public int ConditionNegativeSaturation { get; set; }
        public int ConditionDeadBand { get; set; }

        // Periodic effect (Square / Sine / Triangle / Sawtooth) parameters
        public int PeriodicPeriodMs { get; set; }
        public int PeriodicPhase { get; set; }

        // Envelope, applies to any effect type that has one
        public int EnvelopeAttackLevel { get; set; }
        public int EnvelopeAttackTimeMs { get; set; }
        public int EnvelopeFadeLevel { get; set; }
        public int EnvelopeFadeTimeMs { get; set; }

        /// <summary>
        /// Only populated for a <see cref="FfbOperation.SetDeviceGain"/> command, 0-100
        /// </summary>
        public int DeviceGainPercent { get; set; }
    }
}
