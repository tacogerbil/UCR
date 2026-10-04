using System;

namespace HidWizards.UCR.Core.Utilities.AxisHelpers
{
    /// <summary>
    /// Applies a symmetric power curve around axis center: out = sign(x) * |x|^exponent.
    /// Pure and deterministic; no I/O.
    /// An exponent below 1.0 boosts small deflections and flattens large ones (counters a game that
    /// ramps steering up aggressively toward full lock). Above 1.0 softens the center.
    /// Note the curve pivots on axis center (0), so it is meant for bipolar axes such as steering,
    /// not unipolar pedals whose rest position is the axis minimum.
    /// </summary>
    public class ResponseCurveHelper
    {
        public const int LinearPercentage = 100;
        public const int MinPercentage = 10;
        public const int MaxPercentage = 300;

        private double _exponent = 1d;
        private int _percentage = LinearPercentage;

        /// <summary>Exponent expressed as a percentage: 100 = linear, 60 = exponent 0.6.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Outside MinPercentage..MaxPercentage.</exception>
        public int Percentage
        {
            get => _percentage;
            set
            {
                if (value < MinPercentage || value > MaxPercentage)
                    throw new ArgumentOutOfRangeException(nameof(value), $"Must be {MinPercentage}..{MaxPercentage}");
                _percentage = value;
                _exponent = value / 100d;
            }
        }

        public short Apply(short value)
        {
            if (_percentage == LinearPercentage || value == 0) return value;

            // Positive and negative halves have different magnitudes (32767 vs 32768); normalise each by its own.
            var limit = value > 0 ? (double) Constants.AxisMaxValue : -(double) Constants.AxisMinValue;
            var curved = Math.Pow(Math.Abs((double) value) / limit, _exponent) * limit;
            return Functions.ClampAxisRange((int) Math.Round(value > 0 ? curved : -curved));
        }
    }
}
