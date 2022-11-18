using System.Reflection;
using JasperFx.TypeDiscovery;

namespace Widgets5;

public class Widget5Caller
{
    public static Assembly Calling()
    {
        return CallingAssembly.Find();
    }
}