using System.Xml.Linq;
using JetBrains.Annotations;
using PackagesProps.Models;
using Xunit;

namespace PackagesProps.Tests.Models;

[TestSubject(typeof(PackageReference))]
public class PackageReferenceTest
{

    [Fact]
    public void InvalidPackageReference_Does_Not_Throw()
    {
        
    }
}