using System.Linq;
using System.Xml.Linq;

namespace PackagesProps.Models;

/// <summary>
/// A read-only view over a single <c>&lt;PackageReference&gt;</c> element from a project file,
/// exposing the standard MSBuild metadata (<c>Include</c>, <c>Version</c>, <c>VersionOverride</c>,
/// asset flags, etc.) whether they appear as attributes or child elements.
/// </summary>
public class PackageReference
{
    private readonly XElement _packageReference;

    public PackageReference(XElement packageReference)
    {
        _packageReference = packageReference;
    }

    public bool HasInclude => Include != null;
    public bool HasVersion => Version != null;

    public string Name => Include ?? "[UNKOWN]";

    public string? Include => _packageReference.Attribute("Include")?.Value;

    public string? Version => GetAttributeOrElement("Version");

    public string? VersionOverride => GetAttributeOrElement("VersionOverride");

    public string? IncludeAssets => GetAttributeOrElement("IncludeAssets");

    public string? ExcludeAssets => GetAttributeOrElement("ExcludeAssets");

    public string? PrivateAssets => GetAttributeOrElement("PrivateAssets");

    public string? Condition => _packageReference.Attribute("Condition")?.Value;

    public string? GeneratePathProperty => GetAttributeOrElement("GeneratePathProperty");

    public string? Aliases => GetAttributeOrElement("Aliases");

    public string? NoWarn => GetAttributeOrElement("NoWarn");

    private string? GetAttributeOrElement(string name)
    {
        var attribute = _packageReference.Attribute(name)?.Value;
        if (attribute != null)
            return attribute;

        var element = _packageReference.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        return element?.Value;
    }
}