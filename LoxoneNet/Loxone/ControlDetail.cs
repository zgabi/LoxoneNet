using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone;

class ControlDetail
{
    public bool jLockable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string format { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int movementScene { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public GuidAndName masterValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlDetailControl[] controls { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool isAutomatic { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int animation { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement type { get; set; }
}