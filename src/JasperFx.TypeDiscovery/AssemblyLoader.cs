using System.Reflection;

namespace JasperFx.TypeDiscovery;

public static class AssemblyLoader
{
    public static Assembly ByName(string assemblyName)
    {
        return Assembly.Load(new AssemblyName(assemblyName));
    }
}