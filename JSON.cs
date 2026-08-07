using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;

/* JSON is a small value type for ordinary JSON data in memory.

The cases correspond directly to the JSON data model: object, array, string, integer, decimal number, Boolean, and null. Proxy is an additional in-memory case that resolves to JSON whenever a value is read or serialized.

JSON values are copied as values. Objects and arrays are represented by private collections, and mutation through an indexer makes a changed copy. The public Object and Array properties also return detached collection copies. Use the indexers, RemoveValue, or SetPath for approved mutation.

The short constructors and implicit conversions are convenient for simple values. NewObject and NewArray make compound values explicit. Parse and Stringify handle textual JSON, while ToString produces a friendlier display form for logs and examples.
*/

public interface IJSONProxy
{
    // A proxy resolves to ordinary JSON on demand. The default is JSON null.
    JSON ResolveJSONProxy() => JSON.Null;

    // A proxy may accept an attempted replacement of one of its values. The default does nothing.
    void SetJSONProxyValue(JSON key, JSON? value) { }
}

public struct JSON : IEquatable<JSON>
{
    public enum Kind
    {
        Object,
        Array,
        String,
        Int,
        Number,
        Bool,
        Null,
        Proxy
    }

    public sealed class Error : Exception
    {
        public enum Code
        {
            InvalidFoundationValue,
            InvalidNumber,
            InvalidUTF8
        }

        public Code ErrorCode { get; }

        public Error(Code code, string message) : base(message)
        {
            ErrorCode = code;
        }

        public static Error InvalidFoundationValue() => new(
            Code.InvalidFoundationValue,
            "The value is not a JSON-compatible .NET value.");

        public static Error InvalidNumber() => new(
            Code.InvalidNumber,
            "The number is not valid JSON because it is NaN or infinite.");

        public static Error InvalidUTF8() => new(
            Code.InvalidUTF8,
            "The serialized data is not valid UTF-8.");
    }

    private Kind _kind;
    private object? _value;

    // Simple-value constructors preserve the corresponding JSON case.
    public JSON(string value)
    {
        _kind = Kind.String;
        _value = value;
    }

    public JSON(int value) : this((long)value) { }

    public JSON(long value)
    {
        _kind = Kind.Int;
        _value = value;
    }

    public JSON(double value)
    {
        _kind = Kind.Number;
        _value = value;
    }

    public JSON(bool value)
    {
        _kind = Kind.Bool;
        _value = value;
    }

    public JSON(IEnumerable<JSON> values)
    {
        _kind = Kind.Array;
        _value = new List<JSON>(values);
    }

    public JSON(IDictionary<string, JSON> values)
    {
        _kind = Kind.Object;
        _value = new Dictionary<string, JSON>(values);
    }

    public JSON(IJSONProxy proxy)
    {
        _kind = Kind.Proxy;
        _value = proxy ?? throw new ArgumentNullException(nameof(proxy));
    }

    private JSON(Kind kind, object? value)
    {
        _kind = kind;
        _value = value;
    }

    // The canonical JSON null value and the concrete case of this value.
    public static JSON Null => new(Kind.Null, null);
    public Kind Type => _kind;

    // Explicit constructors for compound and simple values. These are useful when code should visibly construct JSON instead of relying on implicit conversions.
    public static JSON NewObject(params (string Key, JSON Value)[] members) =>
        new(members.ToDictionary(member => member.Key, member => member.Value));

    public static JSON NewArray(params JSON[] values) => new(values);
    public static JSON NewString(string value) => new(value);
    public static JSON NewInt(long value) => new(value);
    public static JSON NewNumber(double value) => new(value);
    public static JSON NewBool(bool value) => new(value);
    public static JSON NewNull() => Null;
    public static JSON NewProxy(IJSONProxy value) => new(value);

    // Typed reads return null when this value cannot provide that type. Proxies are resolved for the read; the proxy itself is not replaced.
    public string? String => _kind switch
    {
        Kind.String => (string)_value!,
        Kind.Proxy => Resolve().String,
        _ => null
    };

    public double? Number => _kind switch
    {
        Kind.Int => (long)_value!,
        Kind.Number => (double)_value!,
        Kind.String => double.TryParse((string)_value!, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null,
        Kind.Proxy => Resolve().Number,
        _ => null
    };

    public long? Int => _kind switch
    {
        Kind.Int => (long)_value!,
        Kind.Number => TryExactLong((double)_value!),
        Kind.String => long.TryParse((string)_value!, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null,
        Kind.Proxy => Resolve().Int,
        _ => null
    };

    public bool? Bool => _kind switch
    {
        Kind.Bool => (bool)_value!,
        Kind.Proxy => Resolve().Bool,
        _ => null
    };

    // Object and Array return detached collection copies. Mutating one of these returned collections does not mutate this JSON value.
    public Dictionary<string, JSON>? Object => _kind switch
    {
        Kind.Object => new Dictionary<string, JSON>((Dictionary<string, JSON>)_value!),
        Kind.Proxy => Resolve().Object,
        _ => null
    };

    public List<JSON>? Array => _kind switch
    {
        Kind.Array => new List<JSON>((List<JSON>)_value!),
        Kind.Proxy => Resolve().Array,
        _ => null
    };

    public bool IsNull => _kind switch
    {
        Kind.Null => true,
        Kind.Proxy => Resolve().IsNull,
        _ => false
    };

    // Returns the live proxy only for the Proxy case; otherwise returns null.
    public IJSONProxy? Proxy => _kind == Kind.Proxy ? (IJSONProxy)_value! : null;

    // String-key access reads or mutates an object member. Assigning null removes the member. If this is a proxy, the proxy receives the set.
    public JSON? this[string key]
    {
        get
        {
            if (_kind == Kind.Proxy) return Resolve()[key];
            return _kind == Kind.Object && ((Dictionary<string, JSON>)_value!).TryGetValue(key, out var value)
                ? value
                : (JSON?)null;
        }
        set
        {
            if (_kind == Kind.Proxy)
            {
                ((IJSONProxy)_value!).SetJSONProxyValue(new JSON(key), value);
                return;
            }

            if (_kind != Kind.Object) return;
            var values = new Dictionary<string, JSON>((Dictionary<string, JSON>)_value!);
            if (value.HasValue) values[key] = value.Value;
            else values.Remove(key);
            _value = values;
        }
    }

    // Integer-key access reads or mutates an array element. Assigning null removes the element. If this is a proxy, the proxy receives the set.
    public JSON? this[int index]
    {
        get
        {
            if (_kind == Kind.Proxy) return Resolve()[index];
            if (_kind != Kind.Array) return null;
            var values = (List<JSON>)_value!;
            return index >= 0 && index < values.Count ? values[index] : (JSON?)null;
        }
        set
        {
            if (_kind == Kind.Proxy)
            {
                ((IJSONProxy)_value!).SetJSONProxyValue(new JSON(index), value);
                return;
            }

            if (_kind != Kind.Array) return;
            var values = new List<JSON>((List<JSON>)_value!);
            if (index < 0 || index >= values.Count) return;
            if (value.HasValue) values[index] = value.Value;
            else values.RemoveAt(index);
            _value = values;
        }
    }

    // A path accessor accepts string keys for objects and integer indexes for arrays. It can be used for both reads and writes.
    public JSON? this[params object[] path]
    {
        get => GetPath(path);
        set => SetPath(path, value);
    }

    // Reads an arbitrary path through objects, arrays, and resolving proxies. An invalid key, index, or intermediate value returns null.
    public JSON? GetPath(object[] path)
    {
        var current = this;
        var position = 0;

        while (position < path.Length)
        {
            if (current._kind == Kind.Proxy)
            {
                current = current.Resolve();
                continue;
            }

            if (current._kind == Kind.Object)
            {
                if (path[position] is not string key ||
                    !((Dictionary<string, JSON>)current._value!).TryGetValue(key, out current)) return null;
            }
            else if (current._kind == Kind.Array)
            {
                if (path[position] is not int index) return null;
                var values = (List<JSON>)current._value!;
                if (index < 0 || index >= values.Count) return null;
                current = values[index];
            }
            else
            {
                return null;
            }
            position++;
        }

        return current;
    }

    // Changes an arbitrary path, returning whether the path was accepted. A null value removes the final object member or array element. An empty path replaces this JSON value itself when a value is supplied.
    public bool SetPath(object[] path, JSON? value)
    {
        if (path.Length == 0)
        {
            if (!value.HasValue) return false;
            this = value.Value;
            return true;
        }

        return SetPathInternal(path, 0, value, allowDetachedMutation: true).Accepted;
    }

    private readonly record struct SetPathResult(bool Accepted, bool ReachedLiveProxy);

    private SetPathResult SetPathInternal(
        object[] path,
        int position,
        JSON? value,
        bool allowDetachedMutation)
    {
        if (_kind == Kind.Proxy)
        {
            if (position == path.Length - 1)
            {
                var key = path[position] switch
                {
                    JSON json => json,
                    string text => new JSON(text),
                    int index => new JSON(index),
                    _ => (JSON?)null
                };
                if (!key.HasValue) return new(false, false);
                ((IJSONProxy)_value!).SetJSONProxyValue(key.Value, value);
                return new(true, true);
            }

            var resolved = Resolve();
            return resolved.SetPathInternal(path, position, value, allowDetachedMutation: false);
        }

        if (position >= path.Length)
        {
            if (!value.HasValue) return new(false, false);
            this = value.Value;
            return new(true, false);
        }

        var isFinal = position == path.Length - 1;

        if (_kind == Kind.Object)
        {
            if (path[position] is not string key) return new(false, false);
            var values = new Dictionary<string, JSON>((Dictionary<string, JSON>)_value!);

            if (isFinal)
            {
                if (value.HasValue) values[key] = value.Value;
                else values.Remove(key);
                _value = values;
                return new(true, false);
            }

            if (!values.TryGetValue(key, out var child)) return new(false, false);
            var result = child.SetPathInternal(path, position + 1, value, allowDetachedMutation);
            if (result.Accepted && (allowDetachedMutation || result.ReachedLiveProxy))
            {
                values[key] = child;
                _value = values;
            }
            return result;
        }

        if (_kind == Kind.Array)
        {
            if (path[position] is not int index) return new(false, false);
            var values = new List<JSON>((List<JSON>)_value!);
            if (index < 0 || index >= values.Count) return new(false, false);

            if (isFinal)
            {
                if (value.HasValue) values[index] = value.Value;
                else values.RemoveAt(index);
                _value = values;
                return new(true, false);
            }

            var child = values[index];
            var result = child.SetPathInternal(path, position + 1, value, allowDetachedMutation);
            if (result.Accepted && (allowDetachedMutation || result.ReachedLiveProxy))
            {
                values[index] = child;
                _value = values;
            }
            return result;
        }

        return new(false, false);
    }

    public JSON? RemoveValue(string key)
    {
        if (_kind != Kind.Object) return null;
        var values = new Dictionary<string, JSON>((Dictionary<string, JSON>)_value!);
        if (!values.Remove(key, out var oldValue)) return null;
        _value = values;
        return oldValue;
    }

    // Resolves a proxy once for this operation. Resolution is not cached, so a proxy may return a different JSON value each time.
    public JSON Resolve() => _kind == Kind.Proxy
        ? ((IJSONProxy)_value!).ResolveJSONProxy()
        : this;

    // Equality is deep for objects and arrays. Proxies are resolved before comparison, so a changing proxy need not equal itself across reads.
    public bool Equals(JSON other)
    {
        var left = this;
        var right = other;
        if (left._kind == Kind.Proxy) return left.Resolve().Equals(right);
        if (right._kind == Kind.Proxy) return left.Equals(right.Resolve());
        if (left._kind != right._kind) return false;

        return left._kind switch
        {
            Kind.Object => ObjectsEqual((Dictionary<string, JSON>)left._value!, (Dictionary<string, JSON>)right._value!),
            Kind.Array => ((List<JSON>)left._value!).SequenceEqual((List<JSON>)right._value!),
            Kind.String => (string)left._value! == (string)right._value!,
            Kind.Int => (long)left._value! == (long)right._value!,
            Kind.Number => (double)left._value! == (double)right._value!,
            Kind.Bool => (bool)left._value! == (bool)right._value!,
            Kind.Null => true,
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is JSON other && Equals(other);

    public override int GetHashCode()
    {
        var resolved = _kind == Kind.Proxy ? Resolve() : this;
        var hash = new HashCode();
        hash.Add(resolved._kind);
        switch (resolved._kind)
        {
            case Kind.Object:
                foreach (var pair in ((Dictionary<string, JSON>)resolved._value!).OrderBy(pair => pair.Key))
                {
                    hash.Add(pair.Key);
                    hash.Add(pair.Value);
                }
                break;
            case Kind.Array:
                foreach (var value in (List<JSON>)resolved._value!) hash.Add(value);
                break;
            default:
                hash.Add(resolved._value);
                break;
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(JSON left, JSON right) => left.Equals(right);
    public static bool operator !=(JSON left, JSON right) => !left.Equals(right);

    // ToString is a friendly display form, not necessarily valid JSON: strings are unquoted and simple object keys may be unquoted.
    public override string ToString() => Render(false);

    private string Render(bool quoteStrings)
    {
        if (_kind == Kind.Proxy) return Resolve().Render(quoteStrings);
        return _kind switch
        {
            Kind.String => quoteStrings ? Quote((string)_value!) : (string)_value!,
            Kind.Int => ((long)_value!).ToString(CultureInfo.InvariantCulture),
            Kind.Number => ((double)_value!).ToString("R", CultureInfo.InvariantCulture),
            Kind.Bool => (bool)_value! ? "true" : "false",
            Kind.Null => "null",
            Kind.Object => RenderObject(),
            Kind.Array => RenderArray(),
            _ => "null"
        };
    }

    private string RenderObject()
    {
        var values = (Dictionary<string, JSON>)_value!;
        if (values.Count == 0) return "{}";
        return "{ " + string.Join(", ", values.OrderBy(pair => pair.Key)
            .Select(pair => RenderKey(pair.Key) + ": " + pair.Value.Render(true))) + " }";
    }

    private string RenderArray()
    {
        var values = (List<JSON>)_value!;
        return values.Count == 0 ? "[]" : "[" + string.Join(", ", values.Select(value => value.Render(true))) + "]";
    }

    private static string RenderKey(string key) => IsJavaScriptIdentifier(key) ? key : Quote(key);

    private static bool IsJavaScriptIdentifier(string value)
    {
        if (value.Length == 0) return false;
        static bool Start(char c) => c == '_' || c == '$' || c is >= 'A' and <= 'Z' || c is >= 'a' and <= 'z';
        static bool Continue(char c) => Start(c) || c is >= '0' and <= '9';
        return Start(value[0]) && value.Skip(1).All(Continue);
    }

    private static string Quote(string value) => JsonSerializer.Serialize(value);

    // Parse converts textual JSON into its corresponding in-memory value.
    public static JSON Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        return FromElement(document.RootElement);
    }

    // Stringify resolves proxies recursively and writes valid JSON text. It throws Error.InvalidNumber for NaN or infinity.
    public static string Stringify(JSON value, bool prettyPrinted = true)
    {
        var resolved = ResolveForSerialization(value);
        var builder = new StringBuilder();
        WriteJson(resolved, builder, prettyPrinted, 0);
        return builder.ToString();
    }

    // Converts common .NET values, dictionaries, and enumerables into JSON. Unsupported values or non-string dictionary keys throw a JSON Error.
    public static JSON FromObject(object? value)
    {
        if (value is null) return Null;
        if (value is JSON json) return json;
        if (value is string text) return new JSON(text);
        if (value is bool boolean) return new JSON(boolean);
        if (value is byte or short or int or long) return new JSON(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        if (value is float or double or decimal) return new JSON(Convert.ToDouble(value, CultureInfo.InvariantCulture));
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, JSON>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is not string key) throw Error.InvalidFoundationValue();
                result[key] = FromObject(entry.Value);
            }
            return new JSON(result);
        }
        if (value is IEnumerable sequence)
        {
            var result = new List<JSON>();
            foreach (var item in sequence) result.Add(FromObject(item));
            return new JSON(result);
        }
        throw Error.InvalidFoundationValue();
    }

    private static JSON FromElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => new JSON(element.EnumerateObject().ToDictionary(property => property.Name, property => FromElement(property.Value))),
        JsonValueKind.Array => new JSON(element.EnumerateArray().Select(FromElement).ToList()),
        JsonValueKind.String => new JSON(element.GetString()!),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => new JSON(integer),
        JsonValueKind.Number => new JSON(element.GetDouble()),
        JsonValueKind.True => new JSON(true),
        JsonValueKind.False => new JSON(false),
        JsonValueKind.Null => Null,
        _ => throw new JsonException("Unsupported JSON value.")
    };

    private static JSON ResolveForSerialization(JSON value)
    {
        if (value._kind == Kind.Proxy) return ResolveForSerialization(value.Resolve());
        if (value._kind == Kind.Number && !double.IsFinite((double)value._value!)) throw Error.InvalidNumber();
        if (value._kind == Kind.Object)
        {
            var result = new Dictionary<string, JSON>();
            foreach (var pair in (Dictionary<string, JSON>)value._value!) result[pair.Key] = ResolveForSerialization(pair.Value);
            return new JSON(result);
        }
        if (value._kind == Kind.Array)
        {
            return new JSON(((List<JSON>)value._value!).Select(ResolveForSerialization).ToList());
        }
        return value;
    }

    private static void WriteJson(JSON value, StringBuilder builder, bool pretty, int indent)
    {
        if (value._kind == Kind.String)
        {
            builder.Append(Quote((string)value._value!));
            return;
        }
        if (value._kind is Kind.Int or Kind.Number or Kind.Bool)
        {
            builder.Append(value.Render(false));
            return;
        }
        if (value._kind == Kind.Null)
        {
            builder.Append("null");
            return;
        }
        if (value._kind == Kind.Array)
        {
            var values = (List<JSON>)value._value!;
            if (values.Count == 0) { builder.Append("[]"); return; }
            builder.Append('[');
            if (pretty) builder.Append('\n');
            for (var index = 0; index < values.Count; index++)
            {
                if (pretty) AppendIndent(builder, indent + 1);
                WriteJson(values[index], builder, pretty, indent + 1);
                if (index + 1 < values.Count) builder.Append(',');
                if (pretty) builder.Append('\n');
            }
            if (pretty) AppendIndent(builder, indent);
            builder.Append(']');
            return;
        }
        if (value._kind == Kind.Object)
        {
            var values = (Dictionary<string, JSON>)value._value!;
            if (values.Count == 0) { builder.Append("{}"); return; }
            builder.Append('{');
            if (pretty) builder.Append('\n');
            var ordered = values.OrderBy(pair => pair.Key).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                if (pretty) AppendIndent(builder, indent + 1);
                builder.Append(Quote(ordered[index].Key)).Append(pretty ? ": " : ":");
                WriteJson(ordered[index].Value, builder, pretty, indent + 1);
                if (index + 1 < ordered.Length) builder.Append(',');
                if (pretty) builder.Append('\n');
            }
            if (pretty) AppendIndent(builder, indent);
            builder.Append('}');
        }
    }

    private static void AppendIndent(StringBuilder builder, int level) => builder.Append(' ', level * 2);

    private static long? TryExactLong(double value)
    {
        if (!double.IsFinite(value) || value < long.MinValue || value > long.MaxValue) return null;
        var integer = (long)value;
        return integer == value ? integer : null;
    }

    private static bool ObjectsEqual(Dictionary<string, JSON> left, Dictionary<string, JSON> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || pair.Value != value) return false;
        }
        return true;
    }

    public static implicit operator JSON(string value) => new(value);
    public static implicit operator JSON(int value) => new(value);
    public static implicit operator JSON(long value) => new(value);
    public static implicit operator JSON(double value) => new(value);
    public static implicit operator JSON(bool value) => new(value);
}
