using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone;

class Control
{
    public string name { get; set; }

    public string type { get; set; }

    public GuidAndName uuidAction { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? room { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? cat { get; set; }

    public int defaultRating { get; set; }

    public bool isFavorite { get; set; }

    public bool isSecured { get; set; }

    public string defaultIcon { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlConfigState configState { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlDetail details { get; set; }

    public ControlStates states { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ControlStatistic statistic { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<GuidAndName, Control>? subControls { get; set; }

    [JsonIgnore]
    public LoxAPP3 LoxApp3 { get; set; }

    [JsonIgnore]
    public Room Room => LoxApp3.rooms[room!.Value];

    [JsonIgnore]
    public Category Category => LoxApp3.cats[cat!.Value];
}
