using System;
using System.IO;
using HidWizards.UCR.Utilities;
using NUnit.Framework;

namespace HidWizards.UCR.Tests.UtilityTests
{
    [TestFixture]
    internal class SiblingAssemblyResolverTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "UCR-SiblingAssemblyResolverTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void FindSiblingAssemblyFile_DependencyInSubfolder_ReturnsItsPath()
        {
            var providerFolder = Path.Combine(_root, "Core_vJoyInterfaceWrap");
            Directory.CreateDirectory(providerFolder);
            var expectedPath = Path.Combine(providerFolder, "vJoyInterfaceWrap.dll");
            File.WriteAllText(expectedPath, "not a real assembly, just needs to exist");

            var result = SiblingAssemblyResolver.FindSiblingAssemblyFile(_root, "vJoyInterfaceWrap.dll");

            Assert.That(result, Is.EqualTo(expectedPath));
        }

        [Test]
        public void FindSiblingAssemblyFile_NoMatchingFileInAnySubfolder_ReturnsNull()
        {
            Directory.CreateDirectory(Path.Combine(_root, "Core_Interception"));

            var result = SiblingAssemblyResolver.FindSiblingAssemblyFile(_root, "vJoyInterfaceWrap.dll");

            Assert.That(result, Is.Null);
        }

        [Test]
        public void FindSiblingAssemblyFile_RootDirectoryDoesNotExist_ReturnsNull()
        {
            var result = SiblingAssemblyResolver.FindSiblingAssemblyFile(Path.Combine(_root, "Missing"), "vJoyInterfaceWrap.dll");

            Assert.That(result, Is.Null);
        }
    }
}
