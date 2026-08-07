using System.Text;

// Examples of JSON.swift use with assertions for testing.

static class Program
{
    private static void Assert(bool condition, string message = "Assertion failed.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static JSON? ValueAt(JSON tree, IEnumerable<string> path)
    {
        var current = tree;
        foreach (var component in path)
        {
            var next = current[component];
            if (!next.HasValue) return null;
            current = next.Value;
        }
        return current;
    }

    private sealed class Incrementer : IJSONProxy
    {
        private int value;

        public JSON ResolveJSONProxy()
        {
            value += 1;
            return new JSON(value);
        }
    }

    private sealed class SettingsProxy : IJSONProxy
    {
        public string Title { get; set; } = "Initial";
        public int Skip { get; set; } = 1;

        public JSON ResolveJSONProxy() => JSON.NewObject(
            ("title", Title),
            ("skip", Skip));

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

    private sealed class StableProxy : IJSONProxy
    {
        public JSON ResolveJSONProxy() => JSON.NewObject(("value", 1));
    }

    private sealed record Point(double X, double Y)
    {
        public JSON Json => JSON.NewObject(("x", X), ("y", Y));
    }

    private static void Main()
    {
		// there are two different ways to construct most of the types, equally valid.
        Assert(new JSON("Hello World") == JSON.NewString("Hello World"));
        Assert(new JSON(42) == JSON.NewInt(42));
        Assert(new JSON(3.14) == JSON.NewNumber(3.14));
        Assert(new JSON(true) == JSON.NewBool(true));
        Assert(new JSON(false) == JSON.NewBool(false));
        Assert(JSON.Null == JSON.NewNull());
        Assert(new JSON(new[] { new JSON(1), new JSON(2), new JSON(3) }) ==
               JSON.NewArray(new JSON(1), new JSON(2), new JSON(3)));
        Assert(new JSON(new Dictionary<string, JSON>
        {
            ["name"] = new JSON("Abel"),
            ["age"] = new JSON(46)
        }) == JSON.NewObject(("name", new JSON("Abel")), ("age", new JSON(46))));

        // WriteLine automatically calls .ToString(), so these output human-readable non-strict strings.

        Console.WriteLine(new JSON("Hello World"));                 // Hello World
        Console.WriteLine(new JSON(42));                            // 42
        Console.WriteLine(new JSON(3.14));                          // 3.14
        Console.WriteLine(new JSON(true));                          // true
        Console.WriteLine(new JSON(false));                         // false
        Console.WriteLine(JSON.NewArray(new JSON(1), new JSON(2), new JSON(3))); // [1, 2, 3]
        Console.WriteLine(JSON.NewObject(("a", new JSON(1)), ("b", new JSON(true)), ("c", new JSON("cat")))); // { a: 1, b: true, c: "cat" }
        Console.WriteLine(JSON.Null);                               // null

        // the accessors will produce the raw C# value if desired.

        Console.WriteLine(new JSON("Hello World").String);           // Hello World
        Console.WriteLine(new JSON(42).Int);                        // 42
        Console.WriteLine(new JSON(3.14).Number);                   // 3.14
        Console.WriteLine(new JSON(true).Bool);                     // True
        Console.WriteLine(new JSON(false).Bool);                    // False
        Console.WriteLine(JSON.NewArray(new JSON(1), new JSON(2), new JSON(3)).Array); // System.Collections.Generic.List`1[JSON]
        Console.WriteLine(JSON.NewObject(("a", new JSON(1)), ("b", new JSON(true)), ("c", new JSON("cat"))).Object); // System.Collections.Generic.Dictionary`2[System.String,JSON]
        Console.WriteLine(JSON.Null.IsNull);                        // True
        Console.WriteLine(new JSON(1).IsNull);                      // False

        // use .ToString() to casually convert to string, like JavaScript but nicer looking.

        Console.WriteLine(new JSON("Hello World").ToString());      // Hello World
        Console.WriteLine(new JSON(42).ToString());                 // 42
        Console.WriteLine(new JSON(3.14).ToString());               // 3.14
        Console.WriteLine(new JSON(true).ToString());               // true
        Console.WriteLine(new JSON(false).ToString());              // false
        Console.WriteLine(JSON.NewArray(new JSON(1), new JSON(2), new JSON(3)).ToString()); // [1, 2, 3]
        Console.WriteLine(JSON.NewObject(                          // { a: 1, b: true, c: "cat", d: [7, 8, 9], e: { moon: "bright" } }
            ("a", new JSON(1)), ("b", new JSON(true)), ("c", new JSON("cat")),
            ("d", JSON.NewArray(new JSON(7), new JSON(8), new JSON(9))),
            ("e", JSON.NewObject(("moon", new JSON("bright"))))).ToString());
        Console.WriteLine(JSON.Null.ToString());                     // null

        // use JSON.Stringify() to produce standard textual JSON. The optional prettyPrinted adds whitespace.

        try
        {
            Console.WriteLine(JSON.Stringify(new JSON("Hello World"), prettyPrinted: true)); // "Hello World"
            Console.WriteLine(JSON.Stringify(new JSON(42), prettyPrinted: true));            // 42
            Console.WriteLine(JSON.Stringify(new JSON(3.14), prettyPrinted: true));          // 3.14
            Console.WriteLine(JSON.Stringify(new JSON(true), prettyPrinted: true));          // true
            Console.WriteLine(JSON.Stringify(new JSON(false), prettyPrinted: true));         // false
            Console.WriteLine(JSON.Stringify(JSON.NewArray(new JSON(1), new JSON(2), new JSON(3)), prettyPrinted: true)); // multiple lines of valid JSON
            Console.WriteLine(JSON.Stringify(JSON.NewObject( // multiple lines of valid JSON
                ("a", new JSON(1)), ("b", new JSON(true)), ("c", new JSON("cat")),
                ("d", JSON.NewArray(new JSON(7), new JSON(8), new JSON(9))),
                ("e", JSON.NewObject(("moon", new JSON("bright"))))), prettyPrinted: true));
            Console.WriteLine(JSON.Stringify(JSON.Null, prettyPrinted: true)); // null

            // Double.NaN and double.PositiveInfinity cannot formally serialize and throw a JSON.Error instead which we can catch.

            Console.WriteLine(JSON.Stringify(new JSON(double.NaN + double.PositiveInfinity), prettyPrinted: true));

            // There are two other JSON.Error cases; one is not usually seen in C#, when serialized text cannot be represented as UTF-8.
            // The other is when directly constructing JSON.FromObject(object) from a value that we don't support. Neither should come up in practice.
        }
        catch (JSON.Error error)
        {
            Console.WriteLine("Could not serialize: " + error.Message);
        }

        // Example 1. Configuration: JSON is convenient for small, loosely typed settings.

        var configuration = JSON.NewObject(
            ("theme", new JSON("dark")),
            ("fontSize", new JSON(14)),
            ("notifications", new JSON(true)));
        Assert(configuration["theme"]?.String == "dark");
        Assert(configuration.GetPath(["theme"])?.String == "dark");
        Assert(configuration["fontSize"]?.Int == 14);

        // Example 2. API request data: JSON can form a portable request body.

        var requestBody = JSON.NewObject(
            ("username", new JSON("ada")),
            ("email", new JSON("ada@example.com")));
        var requestText = JSON.Stringify(requestBody, prettyPrinted: false);
        Assert(requestText.Contains("ada@example.com"));
        Assert(requestBody.GetPath(["email"])?.String == "ada@example.com");

        // Example 3. API responses: optional subscripts handle absent or changing fields.

        var response = JSON.NewObject(
            ("user", JSON.NewObject(("name", new JSON("Ada")), ("active", new JSON(true)))));
        Assert(response["user"]?["name"]?.String == "Ada");
        Assert(response["user", "name"]?.String == "Ada");
        Assert(response.GetPath(["user", "name"])?.String == "Ada");
        Assert(response["user"] is { } responseUser &&
               responseUser.Object is { } responseObject &&
               !responseObject.ContainsKey("missing"));
        Assert(response.GetPath(["user", "missing"]) is null);

        // Example 4. Preferences: serialized JSON can be stored as ordinary byte data.

        var preferenceText = JSON.Stringify(configuration, prettyPrinted: false);
        var preferenceData = Encoding.UTF8.GetBytes(preferenceText);
        Assert(Encoding.UTF8.GetString(preferenceData) == preferenceText);

        // Example 5. Loading persisted state: Parse reconstructs the same ordinary tree.

        var loadedPreferences = JSON.Parse(Encoding.UTF8.GetString(preferenceData));
        Assert(loadedPreferences == configuration);
        Assert(loadedPreferences.GetPath(["theme"])?.String == "dark");

        // Example 6. Document trees: nested objects and arrays model files, scenes, or views.

        var document = JSON.NewObject(
            ("type", new JSON("folder")),
            ("children", JSON.NewArray(
                JSON.NewObject(("type", new JSON("file")), ("name", new JSON("readme.txt"))),
                JSON.NewObject(("type", new JSON("file")), ("name", new JSON("notes.txt"))))));
        Assert(document["children"]?[1]?["name"]?.String == "notes.txt");
        Assert(document["children", 1, "name"]?.String == "notes.txt");
        Assert(document.GetPath(["children", 1, "name"])?.String == "notes.txt");
		
        // Example 7. Dynamic interchange: a type field can select an interpretation.

        var dynamicItem = JSON.NewObject(("type", new JSON("image")), ("width", new JSON(640)), ("height", new JSON(480)));
        if (dynamicItem["type"]?.String is { } kind)
        {
            Assert(kind == "image");
        }
        Assert(dynamicItem["width"]?.Int == 640);
        Assert(dynamicItem.GetPath(["width"])?.Int == 640);

        // Example 8. Partial updates: change only the members that an operation addresses.

        var applicationState = JSON.NewObject(
            ("user", JSON.NewObject(("name", new JSON("Ada")))),
            ("debug", new JSON(false)));
        var user = applicationState["user"]!.Value;
        user["name"] = new JSON("Grace");
        applicationState["user"] = user;
        applicationState["debug"] = new JSON(true);
        Assert(applicationState.SetPath(["user", "name"], new JSON("Grace")));
        Assert(applicationState["user"]?["name"]?.String == "Grace");
        Assert(applicationState["user", "name"]?.String == "Grace");
        Assert(applicationState["debug"]?.Bool == true);

        // Example 9. Schema validation: accept a dynamic object only when required fields fit.

        var candidate = JSON.NewObject(("id", new JSON(17)), ("name", new JSON("Record")));
        var isValidRecord = candidate["id"]?.Int is not null &&
                            candidate["name"]?.String is not null;
        Assert(isValidRecord);
        Assert(candidate.GetPath(["id"])?.Int == 17);

        // Example 10. Domain serialization: a C# value can expose a JSON representation.

        var point = new Point(2.5, 4);
        Assert(point.Json["x"]?.Number == 2.5);
        Assert(point.Json.GetPath(["x"])?.Number == 2.5);

        // Example 11. Event logs: JSON can describe actions without defining an event class.

        var renameEvent = JSON.NewObject(
            ("action", new JSON("rename")),
            ("path", JSON.NewArray(new JSON("document"), new JSON("title"))),
            ("value", new JSON("New title")));
        Assert(renameEvent["action"]?.String == "rename");
        Assert(renameEvent["path"]?[1]?.String == "title");
        Assert(renameEvent.GetPath(["path", 1])?.String == "title");

        // Example 12. Synchronization: compare a received state with the local state.

        var localState = JSON.NewObject(("version", new JSON(3)), ("online", new JSON(true)));
        var receivedState = JSON.NewObject(("version", new JSON(3)), ("online", new JSON(true)));
        Assert(localState == receivedState);
        Assert(receivedState.GetPath(["version"])?.Int == 3);

        // Example 13. Paths: a simple path can identify a location independently of its tree.

        var path = new[] { "user", "settings", "theme" };
        var themedState = JSON.NewObject(
            ("user", JSON.NewObject(
                ("settings", JSON.NewObject(("theme", new JSON("dark")))))));
        Assert(ValueAt(themedState, path)?.String == "dark");
        Assert(themedState.GetPath(path)?.String == "dark");
        Assert(themedState.GetPath(["user", "settings", "theme"])?.String == "dark");

        // Example 14. Patches: operations can be represented as data and applied explicitly.

        var patch = JSON.NewObject(
            ("action", new JSON("replace")),
            ("path", JSON.NewArray(new JSON("debug"))),
            ("value", new JSON(true)));
        var patchedState = JSON.NewObject(("debug", new JSON(false)));
        if (patch["action"]?.String == "replace" &&
            patch["path"]?[0]?.String is { } key &&
            patch["value"] is { } patchValue)
        {
            patchedState[key] = patchValue;
            Assert(patchedState.SetPath([key], patchValue));
        }
        Assert(patchedState["debug"]?.Bool == true);

        // Example 15. Live values: proxies provide changing or externally owned JSON values.

        var liveCounterObject = new Incrementer();
        var liveCounter = JSON.NewProxy(liveCounterObject);
        Assert(liveCounter.Proxy is not null);
        Assert(liveCounter.Int == 1);
        Assert(liveCounter.Int == 2);
        Assert(JSON.Stringify(liveCounter, prettyPrinted: false) == "3");
        Assert(!liveCounter.Equals(liveCounter));

        // Example 16. Reactive state: a proxy can be a controlled state boundary inside a tree.

        var reactiveSettingsObject = new SettingsProxy();
        var reactiveState = JSON.NewObject(("settings", JSON.NewProxy(reactiveSettingsObject)));
        Assert(reactiveState["settings"]?["title"]?.String == "Initial");
        Assert(reactiveState["settings", "title"]?.String == "Initial");
        Assert(reactiveState.GetPath(["settings", "title"])?.String == "Initial");
        var settingsValue = reactiveState["settings"]!.Value;
        settingsValue["skip"] = new JSON(5);
        Assert(reactiveState.SetPath(["settings", "skip"], new JSON(5)));
        Assert(reactiveSettingsObject.Skip == 5);
        Assert(JSON.Stringify(reactiveState, prettyPrinted: false).Contains("\"skip\":5"));

        // Example 17. Tests resolve-based equality for stable and stateful proxies.

        Assert(JSON.NewProxy(new StableProxy()) == JSON.NewProxy(new StableProxy()));
        Assert(JSON.NewProxy(new StableProxy()).GetPath([])?["value"]?.Int == 1);

        // Extra, simple loose assertions made during testing.

        var person = JSON.NewObject(
            ("name", new JSON("Ada")),
            ("age", new JSON(36)),
            ("admin", new JSON(true)),
            ("tags", JSON.NewArray(new JSON("math"), new JSON("programming"))),
            ("middleName", JSON.Null));
        Assert(person["name"]?.String == "Ada");
        Assert(person.GetPath(["name"])?.String == "Ada");
        Assert(person["age"]?.Int == 36);
        Assert(person["tags"]?[0]?.String == "math");
        Assert(person["tags", 0]?.String == "math");
        Assert(person.GetPath(["tags", 0])?.String == "math");
        Assert(person["middleName"]?.IsNull == true);
        Assert(person == JSON.NewObject(
            ("middleName", JSON.Null),
            ("tags", JSON.NewArray(new JSON("math"), new JSON("programming"))),
            ("admin", new JSON(true)),
            ("age", new JSON(36)),
            ("name", new JSON("Ada"))));

        // explicit constructors, assignment, removal, and invalid receivers.

        person["age"] = new JSON(37);
        person["admin"] = new JSON(false);
        person["temporary"] = null;
        Assert(person.SetPath(["age"], new JSON(37)));
        Assert(person["age"]?.Int == 37);
        Assert(person["admin"]?.Bool == false);
        Assert(person["temporary"] is null);
        person["middleName"] = null;
        Assert(person["middleName"] is null);
        Assert(person.RemoveValue("admin")?.Bool == false);
        Assert(person.RemoveValue("missing") is null);
        var scalar = new JSON(1);
        scalar["ignored"] = new JSON(2);
        Assert(scalar == new JSON(1));

        // approved JSON mutation separately from a detached copy-on-write snapshot

        var copyOnWrite = JSON.NewObject(("a", new JSON(1)));
        copyOnWrite["a"] = new JSON(2);
        Assert(copyOnWrite["a"]?.Int == 2);
        var detachedObject = copyOnWrite.Object!;
        detachedObject["a"] = new JSON(3);
        Assert(copyOnWrite["a"]?.Int == 2);
        Assert(detachedObject["a"].Int == 3);

        // arrays, indexed mutation, removal, and bounds checks

        var numbers = JSON.NewArray(new JSON(10), new JSON(20), new JSON(30));
        numbers[1] = new JSON(25);
        numbers[0] = null;
        numbers[20] = new JSON(99);
        Assert(numbers == JSON.NewArray(new JSON(25), new JSON(30)));
        Assert(numbers[0]?.Int == 25);

        // direct string to number conversion

        Assert(new JSON("42").Int == 42);
        Assert(new JSON("2.5").Number == 2.5);
        Assert(new JSON("2.5").Int is null);
        Assert(new JSON("not a number").Number is null);

        // whole-value replacement through object and array accessors

        JSON replaceable = JSON.NewObject(("old", new JSON(true)));
        replaceable = JSON.NewObject(("new", new JSON(1)));
        Assert(replaceable["new"]?.Int == 1);
        replaceable = JSON.NewArray(new JSON("a"), new JSON("b"));
        Assert(replaceable[1]?.String == "b");
        replaceable = JSON.Null;
        Assert(replaceable.IsNull);

        // Printing: use ToString for a quick value view and Stringify for formatting

        var printable = JSON.NewObject(("message", new JSON("Hello, JSON")), ("count", new JSON(3)));
        Console.WriteLine("Simple JSON: " + printable.ToString());
        Console.WriteLine("Compact JSON:");
        Console.WriteLine(JSON.Stringify(printable, prettyPrinted: false));
        Console.WriteLine("Pretty JSON:");
        Console.WriteLine(JSON.Stringify(printable, prettyPrinted: true));

        // parsing, compact and pretty serialization, and .NET object bridging

        var compact = JSON.Stringify(person, prettyPrinted: false);
        var restored = JSON.Parse(compact);
        Assert(restored == person);
        Assert(JSON.Parse("42").Int == 42);
        Assert(JSON.Parse("2.5") == new JSON(2.5));
        Assert(JSON.Parse("\"text\"").String == "text");
        Assert(JSON.Parse("true").Bool == true);
        Assert(JSON.Parse("null").IsNull);
        Assert(JSON.Parse("[1, 2, 3]") == JSON.NewArray(new JSON(1), new JSON(2), new JSON(3)));
        Assert(JSON.Parse("{\"a\":1,\"b\":true}") == JSON.NewObject(("a", new JSON(1)), ("b", new JSON(true))));
        var pretty = JSON.Stringify(
            JSON.NewObject(("empty", JSON.NewObject()), ("values", JSON.NewArray())),
            prettyPrinted: true);
        Assert(pretty.Contains("\"empty\""));
        Assert(pretty.Contains("\"values\""));
        Assert(JSON.FromObject(person) == person);
        try
        {
            _ = JSON.FromObject(DateTime.Now);
            Assert(false, "DateTime should not be accepted as JSON.");
        }
        catch (JSON.Error error) when (error.ErrorCode == JSON.Error.Code.InvalidFoundationValue)
        {
        }
        try
        {
            _ = JSON.Stringify(new JSON(double.NaN));
            Assert(false, "NaN should not be accepted as JSON.");
        }
        catch (JSON.Error error) when (error.ErrorCode == JSON.Error.Code.InvalidNumber)
        {
        }

        // If you lived here, you'd be home by now!

        Console.WriteLine("√√√ All JSON tests passed");
    }
}
