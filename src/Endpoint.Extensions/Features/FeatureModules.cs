using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace Endpoint.Extensions;

public static class FeatureModules
{
    public static IReadOnlyList<IFeatureHandlers> Discover(Assembly hostAssembly)
    {
        var assemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        Include(assemblies, hostAssembly);
        foreach (var reference in hostAssembly.GetReferencedAssemblies())
        {
            Include(assemblies, Assembly.Load(reference));
        }

        // The compiler omits a project the host never names. The deps file still lists it.
        foreach (var name in Projects(hostAssembly))
        {
            if (assemblies.ContainsKey(name))
            {
                continue;
            }

            Include(assemblies, Assembly.Load(new AssemblyName(name)));
        }

        return assemblies.Values
            .SelectMany(SafeTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IFeatureHandlers).IsAssignableFrom(type))
            .Select(type => (IFeatureHandlers)Activator.CreateInstance(type)!)
            .ToArray();
    }

    static void Include(Dictionary<string, Assembly> assemblies, Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (!string.IsNullOrEmpty(name))
        {
            assemblies[name] = assembly;
        }
    }

    static IEnumerable<string> Projects(Assembly hostAssembly)
    {
        var depsPath = DepsFile(hostAssembly);
        if (depsPath is null)
        {
            yield break;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
        if (!document.RootElement.TryGetProperty("libraries", out var libraries))
        {
            yield break;
        }

        foreach (var library in libraries.EnumerateObject())
        {
            if (!library.Value.TryGetProperty("type", out var type) || type.GetString() != "project")
            {
                continue;
            }

            var slash = library.Name.IndexOf('/');
            if (slash > 0)
            {
                yield return library.Name[..slash];
            }
        }
    }

    static string? DepsFile(Assembly hostAssembly)
    {
        var directory = Path.GetDirectoryName(hostAssembly.Location);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        var named = Path.Combine(directory, $"{hostAssembly.GetName().Name}.deps.json");
        if (File.Exists(named))
        {
            return named;
        }

        var deps = Directory.GetFiles(directory, "*.deps.json");
        return deps.Length == 1 ? deps[0] : null;
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
