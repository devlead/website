Order: 10
Title: Usage Information
---
^"../../../generatedinput/cake-usage.md"

# Custom arguments

All switches not recognized by Cake will be added to an argument list that is passed to the build script. You can access arguments with the [Argument alias methods](/dsl/arguments).

## Examples

Arguments passed to Cake like this:

```bash
Cake.exe -showstuff=true
```

Can be accessed from the script with the `Argument` alias:

```csharp
Argument<bool>("showstuff", false);
```

<div class="attention attention-note">
    <h4>Note</h4>
    <p>
        The conversion uses <a href="https://msdn.microsoft.com/en-us/library/system.componentmodel.typeconverter">type converters</a> under the hood to convert the string value to the desired type.
    </p>
</div>