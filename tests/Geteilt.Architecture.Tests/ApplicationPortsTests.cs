using System.Reflection;

namespace Geteilt.Architecture.Tests;

/// <summary>
/// An interface Application implements itself sits next to its implementation; one it
/// leaves for someone else to implement is a port. Telling them apart by name would not work —
/// <c>IAccessTokenService</c> is a port despite the suffix — so the tests ask who implements it.
/// </summary>
public sealed class ApplicationPortsTests
{
    private const string PortsNamespace = "Geteilt.Application.Ports";

    private static readonly Type[] ApplicationTypes =
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Geteilt.Application.dll")).GetTypes();

    [Fact]
    public void InterfaceImplementedInApplication_LivesNextToItsImplementation()
    {
        string[] misplaced =
        [
            .. from contract in ApplicationTypes.Where(type => type.IsInterface)
               from implementation in ImplementationsOf(contract)
               where implementation.Namespace != contract.Namespace
               select $"{contract.Name} in {contract.Namespace}, implemented in {implementation.Namespace}"
        ];

        misplaced.Should().BeEmpty("one feature is one folder: its service and the service's interface together");
    }

    [Fact]
    public void InterfaceNothingInApplicationImplements_IsAPort()
    {
        string[] misplaced =
        [
            .. ApplicationTypes
                .Where(type => type.IsInterface && type.IsPublic && !ImplementationsOf(type).Any())
                .Where(type => type.Namespace != PortsNamespace)
                .Select(type => $"{type.Name} in {type.Namespace}")
        ];

        misplaced.Should().BeEmpty("what Application needs from outside is listed in one place, {0}", PortsNamespace);
    }

    private static IEnumerable<Type> ImplementationsOf(Type contract) =>
        ApplicationTypes.Where(type => type is { IsClass: true, IsAbstract: false } && contract.IsAssignableFrom(type));
}
