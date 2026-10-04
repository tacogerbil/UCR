using System;
using HidWizards.UCR.Core.Utilities;
using HidWizards.UCR.Core.Utilities.AxisHelpers;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.UtilityTests.HelperTests
{
    [TestFixture]
    public class ResponseCurveHelperTests
    {
        private static ResponseCurveHelper HelperAt(int percentage) => new ResponseCurveHelper { Percentage = percentage };

        [TestCase(Constants.AxisMaxValue, Constants.AxisMaxValue)]
        [TestCase(Constants.AxisMinValue, Constants.AxisMinValue)]
        [TestCase(0, 0)]
        public void Endpoints_AreFixed_ForAnyExponent(short input, short expected)
        {
            foreach (var percentage in new[] { 10, 60, 100, 200, 300 })
                Assert.That(HelperAt(percentage).Apply(input), Is.EqualTo(expected), $"percentage {percentage}");
        }

        [Test]
        public void Linear_ReturnsInputUnchanged()
        {
            Assert.That(HelperAt(100).Apply(12345), Is.EqualTo((short) 12345));
            Assert.That(HelperAt(100).Apply(-12345), Is.EqualTo((short) -12345));
        }

        [Test]
        public void ExponentBelowOne_BoostsSmallDeflection()
        {
            // 0.5 exponent on a half-deflection: sqrt(0.5) ~= 0.707 of full scale.
            var result = HelperAt(50).Apply(Constants.AxisMaxValue / 2);
            Assert.That(result, Is.InRange(23000, 23300));
        }

        [Test]
        public void ExponentAboveOne_SoftensSmallDeflection()
        {
            Assert.That(HelperAt(200).Apply(Constants.AxisMaxValue / 2), Is.InRange(8100, 8300));
        }

        [Test]
        public void Curve_IsSymmetricAroundCenter()
        {
            var helper = HelperAt(60);
            Assert.That(helper.Apply(-10000), Is.EqualTo((short) -helper.Apply(10000)));
        }

        [TestCase(9)]
        [TestCase(301)]
        [TestCase(0)]
        public void Percentage_OutOfRange_Throws(int percentage)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HelperAt(percentage));
        }
    }
}
