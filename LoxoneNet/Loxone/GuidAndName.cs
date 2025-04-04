using LoxoneNet.Loxone.Converters;

namespace LoxoneNet.Loxone;

public struct GuidAndName
{
    public Guid id { get; set; }

    public string? name { get; set; }

    public GuidAndName(Guid id, string? name = null)
    {
        this.id = id;
        this.name = name;
    }

    public GuidAndName(string id, string? name = null)
    {
        this.id = GuidConverter.StringToGuid(id);
        this.name = name;
    }

    public override string ToString()
    {
        return $"{GuidConverter.GuidToString(id)}/{name}";
    }
}