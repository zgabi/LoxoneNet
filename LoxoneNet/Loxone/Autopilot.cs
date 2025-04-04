namespace LoxoneNet.Loxone;

class Autopilot
{
    public string name { get; set; } = null!;

    public Guid uuidAction { get; set; }

    public AutopilotStates states { get; set; } = null!;
}