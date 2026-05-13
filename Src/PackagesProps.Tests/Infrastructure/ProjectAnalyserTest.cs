using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using JetBrains.Annotations;
using Lombok.NET;
using NSubstitute;
using PackagesProps.Infrastructure;
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
        var analyzePathForProjectWrappers = await subject.AnalyzePathForProjectWrappers(TestConstants.RootTestsPath / "IncorrectVersions").ToListAsync();
        analyzePathForProjectWrappers.Should().HaveCount(2);
    }
    
    [Fact]
    public async Task GetPackageReferences_With_Invalid_Package_Versions_Does_Not_Throw()
    {
        var testHarness = new TestHarness();
        var subject = testHarness.InitSubject();
        var projectWrappers = await subject.AnalyzePathForProjectWrappers(TestConstants.RootTestsPath / "IncorrectVersions").ToListAsync();
        _ = await subject.GetPackageReferences(projectWrappers, null).ToListAsync();
        
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