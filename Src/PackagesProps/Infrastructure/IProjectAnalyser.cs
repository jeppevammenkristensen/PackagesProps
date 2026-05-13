using System.Collections.Generic;
using PackagesProps.Models;
using PackagesProps.ViewModels;
using TruePath;

namespace PackagesProps.Infrastructure;

public interface IProjectAnalyser
{
    IAsyncEnumerable<ProjectWrapper> AnalyzePathForProjectWrappers(AbsolutePath path);
    IAsyncEnumerable<PackageAggregate> GetPackageReferences(IEnumerable<ProjectWrapper> projects, DirectoryPackagesPropsWrapper? packagesPropsWrapper);
}