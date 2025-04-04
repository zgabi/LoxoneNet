using System.Text.Json;

namespace LoxoneNet.Loxone;

public class LoxoneResponseLL
{
    public string control { get; set; }
    
    public JsonElement value { get; set; }
    
    public string code { get; set; }
    
    public string Code
    {
        get => code;
        set => code = value;
    }
}