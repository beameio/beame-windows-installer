using BeameWindowsInstaller;
using NUnit.Framework;

namespace BeameWindowsInstallerTests
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void GetConfigurationValue_ExistingString_Test()
        {
            var rr = Helper.GetConfigurationValue("TestString", "Hello");
            Assert.AreEqual(rr, "Gatekeeper");
        }
        
        [Test]
        public void GetConfigurationValue_DefaultString_Test()
        {
            var rr = Helper.GetConfigurationValue("dsadas", "Hello");
            Assert.AreEqual(rr, "Hello");
        }

        
        [Test]
        public void GetConfigurationValue_ExistingTrue_Test()
        {
            var rr = Helper.GetConfigurationValue("TestBoolTrue", false);
            Assert.True(rr);
        }
        
        [Test]
        public void GetConfigurationValue_ExistingFalse_Test()
        {
            var rr = Helper.GetConfigurationValue("TestBoolFalse", true);
            Assert.False(rr);
        }
        
        [Test]
        public void GetConfigurationValue_ExistingTrueComplex_Test()
        {
            var rr = Helper.GetConfigurationValue("TestBoolTrueComplex", false);
            Assert.True(rr);
        }
        
        [Test]
        public void GetConfigurationValue_ExistingFalseComplex_Test()
        {
            var rr = Helper.GetConfigurationValue("TestBoolFalseComplex", true);
            Assert.False(rr);
        }
        
        [Test]
        public void GetConfigurationValue_DefaultBoolTrue_Test()
        {
            var rr = Helper.GetConfigurationValue("NotExistingProperty", true);
            Assert.True(rr);
        }
        
        [Test]
        public void GetConfigurationValue_DefaultBoolFalse_Test()
        {
            var rr = Helper.GetConfigurationValue("NotExistingProperty", false);
            Assert.False(rr);
        }
    }
}