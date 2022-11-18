using System.Reflection;
using System.Runtime.Loader;

namespace JasperFx.TypeDiscovery;

internal static class BaselineAssemblyContext
{
#if NET461
		public static readonly IBaselineAssemblyLoadContext Loader = new CustomAssemblyLoadContext();
#else
    public static readonly IBaselineAssemblyLoadContext Loader =
        new AssemblyLoadContextWrapper(AssemblyLoadContext.Default);
#endif
}

/// <summary>
///     Utility to discover and load assemblies installed in your application for extensibility or plugin schems
/// </summary>
public static class AssemblyFinder
{
    /// <summary>
    ///     Find assemblies in the application's binary path
    /// </summary>
    /// <param name="logFailure">Take an action when an assembly file could not be loaded</param>
    /// <param name="includeExeFiles">Optionally include *.exe files</param>
    /// <returns></returns>
    public static IEnumerable<Assembly> FindAssemblies(Action<string> logFailure, Func<Assembly, bool> filter,
        bool includeExeFiles)
    {
        string path;
        try
        {
            path = AppContext.BaseDirectory;
        }
        catch (Exception)
        {
            path = Directory.GetCurrentDirectory();
        }

        return FindAssemblies(filter, path, logFailure, includeExeFiles);
    }

    /// <summary>
    ///     Find assemblies in the given path
    /// </summary>
    /// <param name="assemblyPath">The path to probe for assembly files</param>
    /// <param name="logFailure">Take an action when an assembly file could not be loaded</param>
    /// <param name="includeExeFiles">Optionally include *.exe files</param>
    /// <returns></returns>
    public static IEnumerable<Assembly> FindAssemblies(Func<Assembly, bool> filter, string assemblyPath,
        Action<string> logFailure, bool includeExeFiles)
    {
        var assemblies = findAssemblies(assemblyPath, logFailure, includeExeFiles)
            .Where(filter)
            .OrderBy(x => x.GetName().Name)
            .ToArray();

        Assembly[] FindDependencies(Assembly a)
        {
            return assemblies.Where(x => a.GetReferencedAssemblies().Any(_ => _.Name == x.GetName().Name)).ToArray();
        }

        return assemblies.TopologicalSort((Func<Assembly, Assembly[]>)FindDependencies, false);
    }

    private static IEnumerable<Assembly> findAssemblies(string assemblyPath, Action<string> logFailure,
        bool includeExeFiles)
    {
        var dllFiles = Directory.EnumerateFiles(assemblyPath, "*.dll", SearchOption.AllDirectories);
        var files = dllFiles;

        if (includeExeFiles)
        {
            var exeFiles = Directory.EnumerateFiles(assemblyPath, "*.exe", SearchOption.AllDirectories);
            files = dllFiles.Concat(exeFiles);
        }

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            Assembly assembly = null;

            try
            {
                assembly = BaselineAssemblyContext.Loader.LoadFromAssemblyName(new AssemblyName(name));
            }
            catch (Exception)
            {
                try
                {
                    assembly = BaselineAssemblyContext.Loader.LoadFromAssemblyPath(file);
                }
                catch (Exception)
                {
                    logFailure(file);
                }
            }

            if (assembly != null)
            {
                yield return assembly;
            }
        }
    }


    /// <summary>
    ///     Find assembly files matching a given filter
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="onDirectoryFound"></param>
    /// <param name="includeExeFiles"></param>
    /// <returns></returns>
    public static IEnumerable<Assembly> FindAssemblies(Func<Assembly, bool> filter,
        Action<string> onDirectoryFound = null, bool includeExeFiles = false)
    {
        if (filter == null)
        {
            filter = a => true;
        }

        if (onDirectoryFound == null)
        {
            onDirectoryFound = dir => { };
        }

        return FindAssemblies(file => { }, filter, includeExeFiles);
    }
}

internal interface IBaselineAssemblyLoadContext
{
    Assembly LoadFromStream(Stream assembly);
    Assembly LoadFromAssemblyName(AssemblyName assemblyName);
    Assembly LoadFromAssemblyPath(string assemblyName);
}

#if !NET461
public sealed class CustomAssemblyLoadContext : AssemblyLoadContext, IBaselineAssemblyLoadContext
{
    Assembly IBaselineAssemblyLoadContext.LoadFromAssemblyName(AssemblyName assemblyName)
    {
        return Load(assemblyName);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        return Assembly.Load(assemblyName);
    }
}

public sealed class AssemblyLoadContextWrapper : IBaselineAssemblyLoadContext
{
    private readonly AssemblyLoadContext ctx;

    public AssemblyLoadContextWrapper(AssemblyLoadContext ctx)
    {
        this.ctx = ctx;
    }

    public Assembly LoadFromStream(Stream assembly)
    {
        return ctx.LoadFromStream(assembly);
    }

    public Assembly LoadFromAssemblyName(AssemblyName assemblyName)
    {
        return ctx.LoadFromAssemblyName(assemblyName);
    }

    public Assembly LoadFromAssemblyPath(string assemblyName)
    {
        return ctx.LoadFromAssemblyPath(assemblyName);
    }
}
#else
        public class CustomAssemblyLoadContext : IBaselineAssemblyLoadContext
        {
            public Assembly LoadFromStream(Stream assembly)
            {
                if (assembly is MemoryStream memStream)
                {
                    return Assembly.Load(memStream.ToArray());
                }

                using (var stream = new MemoryStream())
                {
                    assembly.CopyTo(stream);
                    return Assembly.Load(stream.ToArray());
                }
            }
		
            Assembly IBaselineAssemblyLoadContext.LoadFromAssemblyName(AssemblyName assemblyName)
            {
                return Assembly.Load(assemblyName);
            }

            public Assembly LoadFromAssemblyPath(string assemblyName)
            {
                return Assembly.LoadFrom(assemblyName);
            }

            public Assembly LoadFromAssemblyName(string assemblyName)
            {
                return Assembly.Load(assemblyName);
            }
        }
#endif

internal static class TopologicalSortExtensions
{
    /// <summary>
    ///     Performs a topological sort on the enumeration based on dependencies
    /// </summary>
    /// <param name="source"></param>
    /// <param name="dependencies"></param>
    /// <param name="throwOnCycle"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static IEnumerable<T> TopologicalSort<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> dependencies,
        bool throwOnCycle = true)
    {
        var sorted = new List<T>();
        var visited = new HashSet<T>();

        foreach (var item in source) Visit(item, visited, sorted, dependencies, throwOnCycle);

        return sorted;
    }

    private static void Visit<T>(T item, ISet<T> visited, ICollection<T> sorted, Func<T, IEnumerable<T>> dependencies,
        bool throwOnCycle)
    {
        if (visited.Contains(item))
        {
            if (throwOnCycle && !sorted.Contains(item))
            {
                throw new Exception("Cyclic dependency found");
            }
        }
        else
        {
            visited.Add(item);

            foreach (var dep in dependencies(item)) Visit(dep, visited, sorted, dependencies, throwOnCycle);

            sorted.Add(item);
        }
    }
}