using System.Text.Json;

namespace LoxoneNet.SmartHome;

class Input
{
    public string intent { get; set; }

    public JsonElement payload { get; set; }
}