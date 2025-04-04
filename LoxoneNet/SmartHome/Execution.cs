using System.Text.Json;

namespace LoxoneNet.SmartHome;

class Execution
{
    public string command { get; set; }
    
    public JsonElement @params { get; set; }
}