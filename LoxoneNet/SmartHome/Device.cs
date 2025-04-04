namespace LoxoneNet.SmartHome;

class Device
{
    public string id { get; set; }
    
    public string type { get; set; }

    public string[] traits { get; set; }
    
    public Name name { get; set; }
    
    public bool willReportState { get; set; }
    
    public bool notificationSupportedByAgent { get; set; }
    
    public string roomHint { get; set; }
    
    public DeviceInfo deviceInfo { get; set; }
    
    public object attributes { get; set; }
    
    public object customData { get; set; }

    public AlternateDeviceId[] otherDeviceIds { get; set; }
}