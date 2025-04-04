using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone;

class ControlStates
{

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid value { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid error { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid jLocked { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid activeMoods { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid moodList { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid activeMoodsNum { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid favoriteMoods { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //aquapublic Guid additionalMoods { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid circuitNames { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid active { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid events { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid infoText { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid up { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid down { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid position { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid min { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid max { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid step { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid shadePosition { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid safetyActive { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid autoAllowed { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid autoActive { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid locked { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid autoInfoText { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid autoState { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid deviceState { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid targetPosition { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid targetPositionLamelle { get; set; }

    //[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    //public Guid adjustingEndPos { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; } = null!;
}