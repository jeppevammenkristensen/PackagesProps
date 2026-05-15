using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JetBrains.Annotations;
using Lombok.NET;
using NSubstitute;
using PackagesProps.Infrastructure;
using PackagesProps.Models;
using TruePath;
using Xunit;

namespace PackagesProps.Tests.Infrastructure;




[TestSubject(typeof(ProjectAnalyser))]
public class ProjectAnalyserTest
{

    [Fact]
    public async Task AnalyzePathForProjectWrappers()
    {
        var testHarness = new TestHarness();
        var subject = testHarness.InitSubject();
        var analyzePathForProjectWrappers = await subject.AnalyzePathForProjectWrappers(TestConstants.RootTestsPath / "IncorrectVersions").ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        analyzePathForProjectWrappers.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task GetPackageReferences_With_Invalid_Package_Versions_Does_Not_Throw()
    {
        var testHarness = new TestHarness();
        var subject = testHarness.InitSubject();
        var projectWrappers = await subject.AnalyzePathForProjectWrappers(TestConstants.RootTestsPath / "IncorrectVersions").ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        _ = await subject.GetPackageReferences(projectWrappers, null).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
    }
    
    [Fact]
    public async Task GetPackageReferences_WithDuplicate_Does_Not_Throw()
    {
        var testHarness = new TestHarness();
        var subject = testHarness.InitSubject();
        var rootTestsPath = TestConstants.RootTestsPath / "DiffVersionsInPackagesProps";
        var propsPath = rootTestsPath / "Directory.Packages.props";
        
        var directoryWrapper = new DirectoryPackagesPropsWrapper(propsPath);
        await directoryWrapper.Load();
        
        var projectWrappers = await subject.AnalyzePathForProjectWrappers(rootTestsPath).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        
        var result = await subject.GetPackageReferences(projectWrappers, directoryWrapper).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var newtonsoftJson = result.FirstOrDefault(x => x.Package == "Newtonsoft.Json");
        newtonsoftJson.Should().NotBeNull();
        newtonsoftJson.PackagePropsVersion.Should().Be("13.0.3");
        newtonsoftJson.HighestProjectsVersion.Should().Be("13.0.1");
        newtonsoftJson.UsedVersion.Should().Be("13.0.3");

    }
}


public static class TestConstants
{
    public static AbsolutePath RootTestsPath => new AbsolutePath(AppContext.BaseDirectory) / "TestSources";
}
[With]
internal partial class TestHarness 
{
    
    public IFileSystem FileSystem { get; } = new FileSystem();
    public IServiceLocator ServiceLocator { get; } = Substitute.For<IServiceLocator>();
    
    public ProjectAnalyser InitSubject()
    {
        return new ProjectAnalyser(FileSystem, ServiceLocator);
    }
} 