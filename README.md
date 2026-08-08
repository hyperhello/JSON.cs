# JSON.cs Quick Guide

`JSON` is a value-oriented C# type representing an ordinary JSON value:

```csharp
struct JSON {
    Object, Array, String, Int, Number, Bool, Null, Proxy
}
```

Use compact literals to construct values:

```csharp
JSON person = JSON.NewObject(
    ("name", "Ada"),
    ("age", 36),
    ("admin", true),
    ("tags", JSON.NewArray( "math", "programming" )),
    ("middleName", JSON.Null));

JSON number = 3.14;
```

## Reading values

Typed accessors return optionals and fail with `null` when the type is wrong:

```csharp
value.String    // string?
value.Int       // long?
value.Number    // double?
value.Bool      // bool?
value.Object    // detached Dictionary<string, JSON>?
value.Array     // detached List<JSON>?
value.IsNull    // bool
```

The `.Int` accessor converts a decimal value only when it has no fractional part.

Object keys and array indexes return `JSON?`:

```csharp
string? name = person["name"]?.String;
long? age = person["age"]?.Int;
string? firstTag = person["tags"]?[0]?.String;
```

## Mutating values

`JSON` has value semantics. Assigning it creates an independent value logically, while arrays and dictionaries benefit from copy-on-write storage.

```csharp
JSON first = JSON.NewObject( ("name", "Abel"), ("age", 46) );
JSON second = first;

second["name"] = "Zippel";
second["age"] = 37;
second["active"] = true;
```

Assigning `null` removes an object key or array element:

```csharp
value["temporary"] = null;
```

Use `JSON.Null` to retain a key or element as JSON null:

```csharp
value["middleName"] = JSON.Null;
```

Array mutation uses zero-based indexes:

```csharp
var numbers = JSON.NewArray([ 10, 20, 30 ]);
numbers[1] = 25;
numbers[0] = null;       // removes the first element
```

Object members can also be removed explicitly:

```csharp
value.RemoveValue("name");
```

`Object` and `Array` return detached snapshots. Mutating one does not mutate the original `JSON`:

```csharp
JSON state = JSON.NewObject();
state["count"] = 1;

var snapshot = state;
state["count"] = 2;
// snapshot["count"] is still 1
```

## Paths

Paths provide one operation for walking through nested objects, arrays, and
proxies. A path component is normally a `string` for an object key or an `int`
for an array index. The ordinary one-component indexers remain the shortest
form:

```csharp
state["user"]
state[0]
```

The variadic indexer accepts mixed components directly:

```csharp
var name = state["users", 1, "name"]?.String;
state["users", 1, "name"] = new JSON("Ada");
```

The explicit methods accept an `object[]` path. Useful when the path is built
dynamically.

```csharp
object[] path = ["users", 1, "name"];
var name = state.GetPath(path)?.String;
bool changed = state.SetPath(path, new JSON("Ada"));
```

## Serialization

`Stringify` produces valid textual JSON:

```csharp
string text = JSON.Stringify(value);
string compact = JSON.Stringify(value, prettyPrinted: false);
JSON restored = JSON.Parse(text);
```

`ToString()` is a friendly JavaScript-like display operation, not strict JSON
serialization:

```csharp
new JSON("Hello World").ToString(); // Hello World
JSON.Stringify(new JSON("Hello World")); // "Hello World"
```

Object keys are sorted during serialization. `NaN` and infinity are rejected
because they are not valid JSON numbers.

## IJSONProxy interface

`IJSONProxy` provides an in-memory value that resolves whenever it is read or serialized. It is represented in textual JSON exactly the same as its resolved JSON and is useful for live or externally owned values:

```csharp
sealed class Counter : IJSONProxy
{
    private int value;

    public JSON ResolveJSONProxy()
    {
        value += 1;
        return new JSON(value);
    }
}

var counter = JSON.NewProxy(new Counter());
counter.Proxy is Counter; // true
counter.Int;              // 1
counter.Int;              // 2
```

An object-shaped proxy can also receive writes to selected keys. It is not required to make the proxy resolve to anything for this.

```csharp
sealed class Settings : IJSONProxy
{
    public string Title { get; set; } = "Initial";
    public int Skip { get; set; } = 1;

    public JSON ResolveJSONProxy() => JSON.NewObject( ("title", new JSON(Title)), ("skip", new JSON(Skip)) );

    public void SetJSONProxyValue(JSON key, JSON? value)
    {
        switch (key.String)
        {
            case "title":
                if (value is { } title) Title = title.String ?? title.ToString();
                break;
            case "skip":
                if (value is { } skip) Skip = (int)(skip.Int ?? Skip);
                break;
        }
    }
}

var settings = JSON.NewProxy(new Settings());
settings["title"] = new JSON("Updated");
settings["skip"] = new JSON(10);
```

Reads resolve proxies as they traverse. Changing a value inside a detached resolved proxy
does not write back unless the proxy provides the appropriate setter behavior.

Proxy resolution is not cached. Proxies may occur inside ordinary objects and
arrays, and may resolve to other proxies. Resolution does not perform cycle
detection.

Equality compares ordinary JSON structurally and deeply. A proxy is resolved
before comparison, so equality involving a stateful proxy may have side effects
or produce different answers at different times.

## .NET object conversion

`JSON.FromObject` accepts JSON-compatible .NET primitives, dictionaries, and
enumerables:

```csharp
var value = JSON.FromObject(new Dictionary<string, object?>
{
    ["name"] = "Ada",
    ["age"] = 36
});
```

Unsupported values throw `JSON.Error`.

## Running the examples

The executable examples and assertions are in `Program.cs`. With the .NET SDK
on the path:

```text
dotnet run
```

## Licensing for use

© 2026 Hypervariety Custom Programming, LLC. All rights reserved.
All commercial and non-commercial use is permitted by the author, as long as
this copyright message accompanies the product and source.
