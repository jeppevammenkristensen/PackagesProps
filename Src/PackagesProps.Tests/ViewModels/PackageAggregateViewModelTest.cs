using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using FileBasedApp.Toolkit.CSharp;
using FluentAssertions;
using JetBrains.Annotations;
using Lombok.NET;
using NSubstitute;
using PackagesProps.Infrastructure;
using PackagesProps.ViewModels;
using TruePath;
using Xunit;

namespace PackagesProps.Tests.ViewModels;

[TestSubject(typeof(PackageAggregateViewModel))]
public class PackageAggregateViewModelTest
{

    [Fact]
    public async Task Refresh_Sets_Correct_Versions()
    {
        var testHarness = new TestHarness();
        var subject = testHarness
            .SetupNugetResult([new PackageResult("Repo1", "TestPackage", [new PackageVersion("1.0.0"), new PackageVersion("2.0.0")])])
            .InitSubject();
        await subject.Refresh(false, new AbsolutePath());
        
        subject.LatestVersions.Should().HaveCount(2);
        subject.LatestVersions.Should().BeEquivalentTo("2.0.0","1.0.0");
    }

    [Fact]
    public async Task Refresh_UsedVersion_Prefers_PackagePropsVersion_OverEverythingElse()
    {
        var subject = new TestHarness()
            .WithPackagePropsVersion("1.5.0")
            .WithHighestInstalledVersion("1.2.0")
            .SetupNugetResult([new PackageResult("Repo1", "TestPackage", [new PackageVersion("1.0.0"), new PackageVersion("2.0.0")])])
            .InitSubject();

        await subject.Refresh(false, new AbsolutePath());

        subject.UsedVersion.Should().Be("1.5.0");
    }

    [Fact]
    public async Task Refresh_UsedVersion_FallsBack_To_HighestProjectsVersion_When_PackagePropsVersion_Is_Null()
    {
        var subject = new TestHarness()
            .WithHighestInstalledVersion("1.2.0")
            .SetupNugetResult([new PackageResult("Repo1", "TestPackage", [new PackageVersion("1.0.0"), new PackageVersion("2.0.0")])])
            .InitSubject();

        await subject.Refresh(false, new AbsolutePath());

        subject.UsedVersion.Should().Be("1.2.0");
    }

    [Fact]
    public async Task Refresh_UsedVersion_FallsBack_To_HighestAvailableVersion_When_NoLocalVersionsKnown()
    {
        var subject = new TestHarness()
            .SetupNugetResult([new PackageResult("Repo1", "TestPackage", [new PackageVersion("1.0.0"), new PackageVersion("2.0.0")])])
            .InitSubject();

        await subject.Refresh(false, new AbsolutePath());

        subject.UsedVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Refresh_UsedVersion_Is_Null_When_No_Sources_Provide_AnyVersion()
    {
        var subject = new TestHarness()
            .SetupNugetResult([])
            .InitSubject();

        await subject.Refresh(false, new AbsolutePath());

        subject.UsedVersion.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_UsedVersion_Picks_HighestAcross_Multiple_Repos()
    {
        var subject = new TestHarness()
            .SetupNugetResult(
            [
                new PackageResult("RepoA", "TestPackage", [new PackageVersion("1.0.0"), new PackageVersion("3.0.0")]),
                new PackageResult("RepoB", "TestPackage", [new PackageVersion("2.0.0"), new PackageVersion("2.5.0")])
            ])
            .InitSubject();

        await subject.Refresh(false, new AbsolutePath());

        subject.UsedVersion.Should().Be("3.0.0");
    }

    [Fact]
    public async Task Refresh_LatestVersions_Does_Not_Contain_Duplicates_When_Repos_Overlap()
    {
        var subject = new TestHarness()
            .SetupNugetResult(
            [
                new PackageResult("RepoA", "TestPackage", [new PackageVersion("1.0.0"), new PackageVersion("2.0.0")]),
                new PackageResult("RepoB", "TestPackage", [new PackageVersion("2.0.0"), new PackageVersion("3.0.0")])
            ])
            .InitSubject();

        await subject.Refresh(false, new AbsolutePath());

        subject.LatestVersions.Should().OnlyHaveUniqueItems();
        subject.LatestVersions.Should().BeEquivalentTo("3.0.0", "2.0.0", "1.0.0");
    }
}

[With]
internal partial class TestHarness
{
    private string _package = "TestPackage";
    private string? _packagePropsVersion = null;
    private string? _highestInstalledVersion  = null;
    private string? _usedVersion = null;
    
    public INugetRepository NugetRepository { get; private set; } = Substitute.For<INugetRepository>();

    public IUiDispatcher UiDispatcher { get; private set; } = new SynchronousUiDispatcher();

    public PackageAggregateViewModel InitSubject()
    {
        var subject = new PackageAggregateViewModel(NugetRepository, UiDispatcher);
        subject.Package = _package;
        subject.PackagePropsVersion = _packagePropsVersion;
        subject.HighestProjectsVersion = _highestInstalledVersion;
        subject.UsedVersion = _usedVersion;
        return subject;
    }

    public TestHarness SetupNugetResult(ImmutableArray<PackageResult> result)
    {
        NugetRepository.FindPackages(_package, Arg.Any<bool>(), Arg.Any<AbsolutePath>())
            .Returns(Task.FromResult(result));
        return this;
    }

}

internal sealed class SynchronousUiDispatcher : IUiDispatcher
{
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}