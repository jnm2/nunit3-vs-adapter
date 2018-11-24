using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

namespace Referencing_NUnit_Engine
{
    public class ReferencingNUnitEngineTests
    {
        [Test]
        public void UsesCorrectVersionOfNUnitEngine()
        {
            const string versionNotUsedByNUnitAdapter = "3.7.0";

            var assembly = typeof(NUnit.Engine.NUnitEngineException)
#if NETCOREAPP1_0
                .GetTypeInfo()
#endif
                .Assembly;

            var versionBlock = FileVersionInfo.GetVersionInfo(assembly.Location);

            Assert.That(versionBlock.ProductVersion, Is.EqualTo(versionNotUsedByNUnitAdapter));
        }
    }
}
