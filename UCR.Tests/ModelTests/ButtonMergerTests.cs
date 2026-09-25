using HidWizards.UCR.Plugins.Remapper;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.ModelTests
{
    [TestFixture]
    public class ButtonMergerTests
    {
        private ButtonMerger _plugin;
        private short _output;

        [SetUp]
        public void Setup()
        {
            // Plugin's constructor already builds Outputs (sized from ButtonMerger's single
            // [PluginOutput] attribute) with Profile null until SetProfile is called; Outputs itself
            // is get-only, so tests hook the existing binding's OutputSink rather than replacing it.
            _output = -1;
            _plugin = new ButtonMerger();
            _plugin.Outputs[0].OutputSink = val => _output = val;
        }

        [Test]
        public void Update_WhenNeitherIsPressed_OutputsZero()
        {
            _plugin.Update(0, 0);

            Assert.That(_output, Is.EqualTo(0));
        }

        [Test]
        public void Update_WhenFirstIsPressed_OutputsOne()
        {
            _plugin.Update(1, 0);

            Assert.That(_output, Is.EqualTo(1));
        }

        [Test]
        public void Update_WhenSecondIsPressed_OutputsOne()
        {
            _plugin.Update(0, 1);

            Assert.That(_output, Is.EqualTo(1));
        }

        [Test]
        public void Update_WhenBothArePressed_OutputsOne()
        {
            _plugin.Update(1, 1);

            Assert.That(_output, Is.EqualTo(1));
        }
    }
}
