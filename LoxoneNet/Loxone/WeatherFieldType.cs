using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone;

class WeatherFieldType
{
    public int id { get; set; }
    
    public string name { get; set; }
    
    public bool analog { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string unit { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string format { get; set; }
}