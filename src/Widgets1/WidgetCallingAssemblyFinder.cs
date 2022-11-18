using System.Reflection;
using JasperFx.TypeDiscovery;

namespace Widgets1;

public class WidgetCallingAssemblyFinder
{
    public static Assembly Calling()
    {
        return CallingAssembly.Find();
    }
}