namespace JasperFx.TypeDiscovery;

public class TypeQuery
{
    private readonly TypeClassification _classification;

    public readonly Func<Type, bool> Filter;

    public TypeQuery(TypeClassification classification, Func<Type, bool> filter = null)
    {
        Filter = filter ?? (t => true);
        _classification = classification;
    }

    public IEnumerable<Type> Find(AssemblyTypes assembly)
    {
        return assembly.FindTypes(_classification).Where(Filter);
    }
}