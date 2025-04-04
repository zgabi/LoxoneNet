using LoxoneNet.Loxone;
using LoxoneNet.Loxone.Converters;

namespace LoxoneNet;

internal record LoxoneDevice(
    LoxAPP3 App,
    string Id,
    string? DeviceName,
    DeviceType DeviceType,
    GuidAndName LoxoneId,
    Control Control,
    Control? ParentControl)
{
    public LoxAPP3 App = App;
    public string Id = Id;
    public string? DeviceName = DeviceName;
    public DeviceType DeviceType = DeviceType;
    public GuidAndName LoxoneId = LoxoneId;
    public Control Control = Control;
    public Control? ParentControl = ParentControl;
    public Dictionary<string, object> States = new Dictionary<string, object>();
    public HashSet<string> StatesToUpdate;

    public string Command => $"jdev/sps/io/{GuidConverter.GuidToString(LoxoneId.id)}/{LoxoneId.name}/";
}