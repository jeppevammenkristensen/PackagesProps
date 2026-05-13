using System.Linq;
using System.Xml.Linq;

namespace PackagesProps.Models;

/// <summary>
/// A read-only view over a single <c>&lt;PackageVersion&gt;</c> element from a
/// <c>Directory.Packages.props</c> file, exposing its <c>Include</c>, <c>Version</c> and
/// <c>Condition</c> values whether they appear as attributes or child elements.
/// </summary>
public class PackageVersionItem
{
    private readonly XElement _packageVersion;

    public PackageVersionItem(XElement packageVersion)
    {
        _packageVersion = packageVersion;
    }

    public bool HasInclude => Include != null;
    public bool HasVersion => Version != null;

    public string Name => Include ?? "[UNKNOWN]";

    public string? Include => _packageVersion.Attribute("Include")?.Value;

    public string? Version => _packageVersion.Attribute("Version")?.Value
                              ?? _packageVersion.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;

    public string? Condition => _packageVersion.Attribute("Condition")?.Value;
}