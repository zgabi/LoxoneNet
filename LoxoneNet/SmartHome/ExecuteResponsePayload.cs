namespace LoxoneNet.SmartHome;

class ExecuteResponsePayload
{
    public string errorCode { get; set; }
    
    public string debugString { get; set; }

    public ExecuteResponseCommand[] commands { get; set; }
}

class QueryResponsePayload
{
    public string errorCode { get; set; }

    public string debugString { get; set; }

    public Dictionary<string, object> devices { get; set; }
}