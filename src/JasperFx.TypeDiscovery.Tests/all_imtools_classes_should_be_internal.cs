using Shouldly;

namespace JasperFx.TypeDiscovery.Tests;

public class all_imtools_classes_should_be_internal
{
    [Fact]
    public void make_it_so()
    {
        var assembly = typeof(TypeQuery).Assembly;
        var types = assembly.DefinedTypes.Where(x => x.IsInNamespace("JasperFx.TypeDiscovery.Util"))
            .Where(x => x.IsPublic)
            .Select(x => x.FullName)
            .ToArray();

        if (types.Any())
        {
            throw new Exception(string.Join("\n", types));
        }

    }
}