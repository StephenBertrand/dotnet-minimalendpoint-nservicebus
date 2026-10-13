using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Endpoint.Extensions;

public static class FeatureModules
{
    public static IReadOnlyList<IFeatureHandlers> Discover(Assembly hostAssembly)
    {
        var assemblies = new List<Assembly> { hostAssembly };
        foreach (var reference in hostAssembly.GetReferencedAssemblies())
        {
            assemblies.Add(Assembly.Load(reference));
        }

        return assemblies
            .SelectMany(SafeTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IFeatureHandlers).IsAssignableFrom(type))
            .Select(type => (IFeatureHandlers)Activator.CreateInstance(type)!)
            .ToArray();
    }

    public static void AddFeatureStorage(this IHostApplicationBuilder builder, Assembly hostAssembly)
    {
        foreach (var feature in Discover(hostAssembly))
        {
            feature.AddStorage(builder);
        }
    }

    public static async Task PrepareFeatureStorageAsync(
        this IHost host,
        Assembly hostAssembly,
        CancellationToken cancellationToken = default)
    {
        foreach (var feature in Discover(hostAssembly))
        {
            await feature.PrepareStorageAsync(host, cancellationToken);
        }
    }

    static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
