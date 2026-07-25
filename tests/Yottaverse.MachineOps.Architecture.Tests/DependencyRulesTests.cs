using System.Reflection;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Desktop.ViewModels;

namespace Yottaverse.MachineOps.Architecture.Tests;

public sealed class DependencyRulesTests
{
    [Fact]
    public void CoreDoesNotReferenceOuterLayers()
    {
        string[] references = GetReferences(typeof(GCodeParser).Assembly);

        Assert.DoesNotContain(references, name => name.StartsWith("Yottaverse.MachineOps.", StringComparison.Ordinal));
    }

    [Fact]
    public void DesktopDoesNotReferencePersistenceOrApiLayers()
    {
        string[] references = GetReferences(typeof(MainViewModel).Assembly);

        Assert.DoesNotContain("Yottaverse.MachineOps.Infrastructure", references);
        Assert.DoesNotContain("Yottaverse.MachineOps.Api", references);
    }

    private static string[] GetReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .OfType<string>()
            .ToArray();
}
