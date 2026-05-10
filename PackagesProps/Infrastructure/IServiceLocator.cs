using System.Threading.Tasks;

namespace PackagesProps.Infrastructure;

public interface IServiceLocator
{
    public T? GetService<T>();
    public T GetRequiredService<T>() where T : notnull;
}

public interface IPageHost
{
    Task AddPage(ScreenPage page, bool activate);
}