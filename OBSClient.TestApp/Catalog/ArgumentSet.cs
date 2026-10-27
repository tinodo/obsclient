using System.Globalization;
using System.Text.Json;
using OBSStudioClient.Classes;

namespace OBSStudioClient.TestApp.Catalog;

/// <summary>Identifies a scene, an input or a source the way the user entered it: by name or by UUID.</summary>
/// <param name="Name">The name, when the object is identified by its name.</param>
/// <param name="Uuid">The UUID, when the object is identified by its UUID.</param>
internal readonly record struct ReferenceValue(string? Name, Guid? Uuid)
{
    /// <inheritdoc/>
    public override string ToString() => this.Uuid is { } uuid ? uuid.ToString() : this.Name ?? string.Empty;
}

/// <summary>
/// The values the user entered for the parameters of a request, by name of the parameter. The generated catalog reads
/// them back with the method that matches the type of the parameter; a parameter that was left out reads as null.
/// </summary>
internal sealed class ArgumentSet
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    /// <summary>Gets the values that were entered.</summary>
    public IReadOnlyDictionary<string, object> Values => this._values;

    /// <summary>Determines whether a value was entered for a parameter.</summary>
    public bool Has(string name) => this._values.ContainsKey(name);

    /// <summary>Sets the value of a parameter.</summary>
    public void Set(string name, object value) => this._values[name] = value;

    public string Text(string name) => this.Required<string>(name);

    public string? OptionalText(string name) => this.OptionalClass<string>(name);

    public int Int(string name) => this.Required<int>(name);

    public int? OptionalInt(string name) => this.OptionalStruct<int>(name);

    public float Float(string name) => this.Required<float>(name);

    public float? OptionalFloat(string name) => this.OptionalStruct<float>(name);

    public bool Bool(string name) => this.Required<bool>(name);

    public bool? OptionalBool(string name) => this.OptionalStruct<bool>(name);

    public Guid Uuid(string name) => this.Required<Guid>(name);

    public Guid? OptionalUuid(string name) => this.OptionalStruct<Guid>(name);

    public T EnumValue<T>(string name) where T : struct, Enum => this.Required<T>(name);

    public T? OptionalEnumValue<T>(string name) where T : struct, Enum => this.OptionalStruct<T>(name);

    public SceneRef Scene(string name)
    {
        var reference = this.Required<ReferenceValue>(name);
        return reference.Uuid is { } uuid ? SceneRef.FromUuid(uuid) : SceneRef.FromName(reference.Name ?? string.Empty);
    }

    public SceneRef? OptionalScene(string name) => this.Has(name) ? this.Scene(name) : (SceneRef?)null;

    public InputRef Input(string name)
    {
        var reference = this.Required<ReferenceValue>(name);
        return reference.Uuid is { } uuid ? InputRef.FromUuid(uuid) : InputRef.FromName(reference.Name ?? string.Empty);
    }

    public InputRef? OptionalInput(string name) => this.Has(name) ? this.Input(name) : (InputRef?)null;

    public SourceRef Source(string name)
    {
        var reference = this.Required<ReferenceValue>(name);
        return reference.Uuid is { } uuid ? SourceRef.FromUuid(uuid) : SourceRef.FromName(reference.Name ?? string.Empty);
    }

    public SourceRef? OptionalSource(string name) => this.Has(name) ? this.Source(name) : (SourceRef?)null;

    public Dictionary<string, object> Map(string name) => this.Deserialize<Dictionary<string, object>>(name);

    public Dictionary<string, object>? OptionalMap(string name) => this.Has(name) ? this.Deserialize<Dictionary<string, object>>(name) : null;

    public JsonElement Element(string name) => this.Required<JsonElement>(name);

    public JsonElement? OptionalElement(string name) => this.OptionalStruct<JsonElement>(name);

    public object Value(string name) => this.Element(name);

    public object? OptionalValue(string name) => this.Has(name) ? this.Element(name) : null;

    public T Json<T>(string name) where T : class => this.Deserialize<T>(name);

    public T? OptionalJson<T>(string name) where T : class => this.Has(name) ? this.Deserialize<T>(name) : null;

    /// <summary>Describes the values, as the call received them.</summary>
    public override string ToString() => string.Join(", ", this._values.Select(pair => $"{pair.Key}: {Display(pair.Value)}"));

    private static string Display(object value) => value switch
    {
        string text => JsonSerializer.Serialize(text),
        JsonElement element => element.GetRawText(),
        ReferenceValue { Uuid: not null } reference => $"uuid {reference}",
        ReferenceValue reference => JsonSerializer.Serialize(reference.Name),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    private T Required<T>(string name)
    {
        if (!this._values.TryGetValue(name, out var value))
        {
            throw new ArgumentException($"A value for '{name}' is required.");
        }

        return value is T typed
            ? typed
            : throw new ArgumentException($"The value of '{name}' is a {value.GetType().Name}, not a {typeof(T).Name}.");
    }

    private T? OptionalClass<T>(string name) where T : class => this._values.TryGetValue(name, out var value) && value is T typed ? typed : null;

    private T? OptionalStruct<T>(string name) where T : struct => this._values.TryGetValue(name, out var value) && value is T typed ? typed : null;

    private T Deserialize<T>(string name) where T : class
    {
        var element = this.Element(name);
        try
        {
            return JsonSerializer.Deserialize<T>(element) ?? throw new ArgumentException($"The value of '{name}' cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"The value of '{name}' is not a valid {typeof(T).Name}: {exception.Message}", exception);
        }
    }
}

/// <summary>Turns the task of any method of the client into a task of an object, for the generated catalog.</summary>
internal static class Invocation
{
    /// <summary>Waits for a call that returns nothing.</summary>
    public static async Task<object?> Of(Task task)
    {
        await task;
        return null;
    }

    /// <summary>Waits for a call that returns a value.</summary>
    public static async Task<object?> Of<T>(Task<T> task) => await task;
}
