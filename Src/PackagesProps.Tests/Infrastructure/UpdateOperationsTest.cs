using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using JetBrains.Annotations;
using Lombok.NET;
using PackagesProps.Infrastructure;
using PackagesProps.Models;
using TruePath;
using Xunit;

namespace PackagesProps.Tests.Infrastructure;

[TestSubject(typeof(UpdateOperations))]
public class UpdateOperationsTest
{
    private const string PropsPath = @"C:\repo\Directory.Packages.props";

    [Fact]
    public async Task UpdateDirectoryPackagePropsFile_SingleVersionEntry_UpdatesVersionInPlace()
    {
        var harness = new UpdateOperationsTestHarness();
        harness.AddPropsFile("""
              <ItemGroup>
                <PackageVersion Include="Serilog" Version="3.1.1" />
              </ItemGroup>
            """);
        var subject = harness.InitSubject();

        await subject.UpdateDirectoryPackagePropsFile(
            new AbsolutePath(PropsPath),
            [new PackageUpdate("Serilog", "3.2.0")]);

        var versions = harness.ReadVersions("Serilog");
        versions.Should().ContainSingle().Which.Should().Be("3.2.0");
    }

    [Fact]
    public async Task UpdateDirectoryPackagePropsFile_MultipleVersionEntries_UpdateToExistingVersion_DoesNotIntroduceDuplicate()
    {
        // Newtonsoft.Json is declared twice: an Include line and an Update line that overrides it.
        var harness = new UpdateOperationsTestHarness();
        harness.AddPropsFile("""
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageVersion Include="Serilog" Version="3.1.1" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Update="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            """);
        var subject = harness.InitSubject();

        await subject.UpdateDirectoryPackagePropsFile(
            new AbsolutePath(PropsPath),
            [new PackageUpdate("Newtonsoft.Json", "13.0.3")]);

        var versions = harness.ReadVersions("Newtonsoft.Json");
        versions.Should().HaveCount(2);
        versions.Should().BeEquivalentTo("13.0.1", "13.0.3");
    }

    [Fact]
    public async Task UpdateDirectoryPackagePropsFile_MultipleVersionEntries_UpdateMatchingTheIncludeEntry_UpdatesThatEntry()
    {
        var harness = new UpdateOperationsTestHarness();
        harness.AddPropsFile("""
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Update="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            """);
        var subject = harness.InitSubject();

        await subject.UpdateDirectoryPackagePropsFile(
            new AbsolutePath(PropsPath),
            [new PackageUpdate("Newtonsoft.Json", "13.0.1")]);

        var versions = harness.ReadVersions("Newtonsoft.Json");
        versions.Should().HaveCount(2).And.OnlyContain(v => v == "13.0.1");
    }

    [Fact]
    public async Task UpdateDirectoryPackagePropsFile_MultipleVersionEntries_NewVersion_AppendsEntryRatherThanUpdating()
    {
        // Documents current behaviour: when a package already has multiple version entries and the
        // requested version matches none of them, a brand-new PackageVersion entry is appended.
        var harness = new UpdateOperationsTestHarness();
        harness.AddPropsFile("""
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Update="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            """);
        var subject = harness.InitSubject();

        await subject.UpdateDirectoryPackagePropsFile(
            new AbsolutePath(PropsPath),
            [new PackageUpdate("Newtonsoft.Json", "14.0.0")]);

        var versions = harness.ReadVersions("Newtonsoft.Json");
        versions.Should().HaveCount(2);
        versions.Should().Contain("14.0.0");
    }

    [Fact]
    public async Task UpdateDirectoryPackagePropsFile_AppliesEveryUpdateInTheBatch()
    {
        var harness = new UpdateOperationsTestHarness();
        harness.AddPropsFile("""
              <ItemGroup>
                <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
                <PackageVersion Include="Serilog" Version="3.1.1" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Update="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            """);
        var subject = harness.InitSubject();

        await subject.UpdateDirectoryPackagePropsFile(
            new AbsolutePath(PropsPath),
            [
                new PackageUpdate("Serilog", "3.2.0"),
                new PackageUpdate("Newtonsoft.Json", "13.0.3")
            ]);

        harness.ReadVersions("Serilog").Should().ContainSingle().Which.Should().Be("3.2.0");
        harness.ReadVersions("Newtonsoft.Json").Should().HaveCount(2);
    }
}

[With]
internal partial class UpdateOperationsTestHarness
{
    private const string PropsPath = @"C:\repo\Directory.Packages.props";

    public MockFileSystem FileSystem { get; } = new();

    public UpdateOperations InitSubject() => new(FileSystem);

    public void AddPropsFile(string itemGroupsXml)
    {
        var content = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
            {itemGroupsXml}
            </Project>
            """;
        FileSystem.AddFile(PropsPath, new MockFileData(content));
    }

    public List<string> ReadVersions(string packageName)
    {
        using var stream = FileSystem.File.OpenRead(PropsPath);
        var root = XElement.Load(stream);
        return root.Descendants("PackageVersion")
            .Where(x => (string?)x.Attribute("Include") == packageName ||
                        (string?)x.Attribute("Update") == packageName)
            .Select(x => (string?)x.Attribute("Version") ?? string.Empty)
            .ToList();
    }
}
