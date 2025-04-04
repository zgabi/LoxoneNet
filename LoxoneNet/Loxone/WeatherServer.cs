namespace LoxoneNet.Loxone;

class WeatherServer
{
    public WeatherServerState states { get; set; }
    
    public WeatherServerFormat format { get; set; }

    public Dictionary<int, string> weatherTypeTexts { get; set; }
    
    public Dictionary<int, WeatherFieldType> weatherFieldTypes { get; set; }
}