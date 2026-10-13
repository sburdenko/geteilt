using System.Text.Json;
using System.Text.Json.Serialization;

namespace Geteilt.Contracts;

/// <summary>
/// How this API's JSON is shaped. Lives in Contracts because the wire format is part of the
/// contract, not an implementation detail of the server: the Blazor and MAUI clients read the same
/// bytes and have to agree about them.
/// </summary>
/// <remarks>
/// Without a single source for this, each side configures its own serializer and they agree right
/// up until one of them does not. That already happened once: the server accepted enums only as
/// numbers while every test passed, because the tests and the server shared one wrong opinion.
/// </remarks>
public static class GeteiltJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        // Enums by name. A number would make every client depend on the declaration order, and
        // reordering the enum would silently change what stored responses mean.
        options.Converters.Add(new JsonStringEnumConverter());
        // Frozen so a caller cannot mutate the shared instance out from under everybody else.
        // The overload matters: the parameterless one refuses without a type resolver, and this
        // supplies the reflection-based default.
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
