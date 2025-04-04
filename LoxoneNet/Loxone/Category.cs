using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone;

class Category
{
    public Guid uuid { get; set; }

    public string name { get; set; }

    public string image { get; set; }

    public int defaultRating { get; set; }

    public bool isFavorite { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool @default { get; set; }

    public string type { get; set; }

    public string color { get; set; }
}