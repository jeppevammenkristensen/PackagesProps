using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using JetBrains.Annotations;
using PackagesProps.Models;
using PackagesProps.Tests.Infrastructure;
using TruePath;
using Xunit;

namespace PackagesProps.Tests.Models;

[TestSubject(typeof(ProjectWrapper))]
public class ProjectWrapperTest
{
    private const string CsprojContent =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
          </ItemGroup>
        </Project>
        """;

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf16LeBom = [0xFF, 0xFE];

    [Fact]
    public async Task GetAllPackageReferences_AllowsIncorrectPackageVersion_Does_Not_Throw()
    {
        var projectWrapper = new ProjectWrapper(TestConstants.RootTestsPath / "IncorrectVersions" / "Project2" / "Project2.csproj");
        await projectWrapper.Load();
        _ = projectWrapper.GetAllPackageReferences().ToList();
    }

    [Fact]
    public async Task Save_PreservesUtf8Bom_WhenLoadedFromFileWithBom()
    {
        await AssertEncodingRoundTrips(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            expectedBom: Utf8Bom);
    }

    [Fact]
    public async Task Save_KeepsFileWithoutBom_WhenLoadedFromFileWithoutBom()
    {
        await AssertEncodingRoundTrips(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            expectedBom: null);
    }

    [Fact]
    public async Task Save_PreservesUtf16Bom_WhenLoadedFromUtf16File()
    {
        await AssertEncodingRoundTrips(
            new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            expectedBom: Utf16LeBom);
    }

    private static async Task AssertEncodingRoundTrips(Encoding encoding, byte[]? expectedBom)
    {
        var token = TestContext.Current.CancellationToken;
        var path = Path.Combine(Path.GetTempPath(), $"ProjectWrapperTest_{Guid.NewGuid():N}.csproj");
        await File.WriteAllTextAsync(path, CsprojContent, encoding, token);
        try
        {
            var wrapper = new ProjectWrapper(new AbsolutePath(path));
            await wrapper.Load();
            await wrapper.Save();

            var bytes = await File.ReadAllBytesAsync(path, token);

            if (expectedBom is null)
            {
                bytes.Should().NotBeEmpty();
                bytes[0].Should().Be((byte)'<',
                    "a file saved without a BOM should start with the XML root element");
            }
            else
            {
                bytes.Take(expectedBom.Length).Should().Equal(expectedBom);
            }

            // The content must still round-trip cleanly after saving.
            var reloaded = new ProjectWrapper(new AbsolutePath(path));
            await reloaded.Load();
            reloaded.GetAllPackageReferences().Select(x => x.Name)
                .Should().ContainSingle().Which.Should().Be("Newtonsoft.Json");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
