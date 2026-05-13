using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PackagesProps.Models;
using PackagesProps.Tests.Infrastructure;
using Xunit;

namespace PackagesProps.Tests.Models;

[TestSubject(typeof(ProjectWrapper))]
public class ProjectWrapperTest
{

    [Fact]
    public async Task GetAllPackageReferences_AllowsIncorrectPackageVersion_Does_Not_Throw()
    {
        var projectWrapper = new ProjectWrapper(TestConstants.RootTestsPath / "IncorrectVersions" / "Project2" / "Project2.csproj");
        await projectWrapper.Load();
        _ = projectWrapper.GetAllPackageReferences().ToList();
    }

    public void Goo()
    {
        
    }
}