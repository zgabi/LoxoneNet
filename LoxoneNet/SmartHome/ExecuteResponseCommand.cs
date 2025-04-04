namespace LoxoneNet.SmartHome;

class ExecuteResponseCommand
{
    public string[] ids { get; set; }

    /// <summary>
    /// SUCCESS, SUCCESS, OFFLINE, EXCEPTIONS, ERROR
    /// </summary>
    public string status { get; set; }

    public object states { get; set; }
    
    public string errorCode { get; set; }
}