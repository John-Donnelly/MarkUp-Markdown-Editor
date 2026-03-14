using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkUp.UITests;

/// <summary>
/// Concrete [TestClass] that owns assembly-level WinAppDriver lifecycle.
/// MSTest only invokes [AssemblyInitialize] / [AssemblyCleanup] on non-abstract [TestClass] types,
/// so this thin shim delegates to the shared helpers in <see cref="AppSession"/>.
/// </summary>
[TestClass]
public sealed class TestAssemblySetup
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _) => AppSession.InitialiseSession();

    [AssemblyCleanup]
    public static void AssemblyCleanup() => AppSession.CleanupSession();
}
