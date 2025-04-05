using System.Text.Json;
using System.Text.Json.Serialization;
using LoxoneNet.Loxone.Converters;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;

namespace LoxoneNet.Loxone;

class LoxAPP3
{
    [JsonConverter(typeof(DateFormatConverter))]
    public DateTime lastModified { get; set; }

    public MsInfo msInfo { get; set; } = null!;

    public Dictionary<string, Guid> globalStates { get; set; } = null!;

    [JsonIgnore]
    public Dictionary<Guid, string> globalStatesByGuid { get; set; } = null!;

    public Dictionary<int, string> operatingModes { get; set; } = null!;

    public Dictionary<Guid, Room> rooms { get; set; } = null!;

    public Dictionary<Guid, Category> cats { get; set; } = null!;

    public Dictionary<Guid, Mode> modes { get; set; } = null!;

    public Dictionary<Guid, Control> controls { get; set; } = null!;

    [JsonIgnore]
    public Dictionary<Guid, ControlState> controlStates { get; set; } = null!;

    public WeatherServer weatherServer { get; set; } = null!;

    public Dictionary<int, TimeInfo> times { get; set; } = null!;

    public Dictionary<Guid, Caller>? caller { get; set; } = null;

    public Dictionary<Guid, Mailer> mailer { get; set; } = null!;

    public Dictionary<Guid, Autopilot> autopilot { get; set; } = null!;

    [JsonIgnore]
    public Dictionary<Guid, (GuidAndName, string)> autopilotStates { get; set; } = null!;

    public Dictionary<Guid, MessageCenter> messageCenter { get; set; } = null!;
    [JsonIgnore]
    public Dictionary<Guid, (GuidAndName, string)> messageCenterStates { get; set; } = null!;


    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; } = null!;

    public readonly LoxoneDevice UnknownDevice;

    public LoxAPP3()
    {
        WebhookProcessor.UnknownDevice = new LoxoneDevice(this,"UNKNOWN", "unknown", DeviceType.Unknown, new GuidAndName(), null, null);
    }

    public void Initialize()
    {
        globalStatesByGuid = globalStates.ToDictionary(x => x.Value, x => x.Key);
        
        controlStates = new Dictionary<Guid, ControlState>();
        foreach (var pair in controls)
        {
            var control = pair.Value;
            control.LoxApp3 = this;
            if (pair.Key != control.uuidAction.id)
            {
                throw new Exception("Control Id mismatch");
            }

            foreach (var controlState in control.states.ExtensionData)
            {
                var guid = GuidConverter.StringToGuid(controlState.Value.GetString()!);
                controlStates.Add(guid, new ControlState(guid, control, controlState.Key));
            }

            var subControls = control.subControls;
            if (subControls != null)
            {
                foreach (var spair in subControls)
                {
                    var subControl = spair.Value;
                    subControl.LoxApp3 = this;
                    if (control.type != "EFM" && control.type != "IRoomControllerV2" && control.type != "Alarm" && control.type != "SmokeAlarm" && control.type != "Intercom"
                        && control.type != "LightControllerV2")
                    {
                        if (spair.Key.id != control.uuidAction.id)
                        {
                            throw new Exception("SubControl Id should be the same as the parent control's id: " + control.type);
                        }
                    }

                    if (spair.Key.id != subControl.uuidAction.id || spair.Key.name != subControl.uuidAction.name)
                    {
                        throw new Exception("Control Id mismatch");
                    }

                    foreach (var controlState in subControl.states.ExtensionData)
                    {
                        var guid = GuidConverter.StringToGuid(controlState.Value.GetString()!);
                        controlStates[guid] = new ControlState(guid, subControl, controlState.Key);
                    }

                    if (subControl.room == null)
                    {
                        subControl.room = control.room;
                    }

                    if (subControl.cat == null)
                    {
                        subControl.cat = control.cat;
                    }
                }
            }
        }

        autopilotStates = new Dictionary<Guid, (GuidAndName, string)>();
        foreach (var auto in autopilot)
        {
            autopilotStates.Add(auto.Value.states.changed, (new GuidAndName(auto.Value.uuidAction), "changed"));
            autopilotStates.Add(auto.Value.states.history, (new GuidAndName(auto.Value.uuidAction), "history"));
        }

        messageCenterStates = new Dictionary<Guid, (GuidAndName, string)>();
        foreach (var mc in messageCenter)
        {
            messageCenterStates.Add(mc.Value.states.changed, (new GuidAndName(mc.Value.uuidAction), "changed"));
        }
    }

    public string? FindGuid(Guid guid, Guid icon, object value)
    {
        if (guid == GuidConverter.StringToGuid("1775ecdd-034c-3f2c-ffff-c759e8dccb8c"))
        {
            return "unknown";
        }

        if (globalStatesByGuid.TryGetValue(guid, out string? state))
        {
            return "STATE: " + state;
        }
        else if (rooms.TryGetValue(guid, out var room0))
        {
            ;
        }
        else if (cats.TryGetValue(guid, out var cat))
        {
            ;
        }
        else if (modes.TryGetValue(guid, out var mode))
        {
            ;
        }
        else if (controls.TryGetValue(guid, out var ctrl))
        {
            StateReceived(ctrl, "value", value, out var device);
            return $"CONTROL: {ctrl.Room.GoogleName}, {ctrl.Category.name}, {ctrl.name}, value: {value} ({value.GetType()})";
        }
        else if (controlStates.TryGetValue(guid, out var ctrlState))
        {
            var control = ctrlState.Control;
            if (StateReceived(control, ctrlState.Name, value, out var device))
            {
                return $"CONTROL STATE: {control.Room.GoogleName}, {control.Category.name}, {control.name}, {ctrlState.Name}, icon: {icon}, value: {value} ({value.GetType()})";
            }

            return null;
        }
        else if (caller?.TryGetValue(guid, out var call) == true)
        {
            ;
        }
        else if (mailer.TryGetValue(guid, out var mail))
        {
            ;
        }
        else if (autopilot.TryGetValue(guid, out var auto))
        {
            ;
        }
        else if (autopilotStates.TryGetValue(guid, out var autoState))
        {
            return "AUTOPILOT STATE: " + autopilot[autoState.Item1.id].name + " " + autoState.Item1.name + " " + autoState.Item2;
        }
        else if (messageCenter.TryGetValue(guid, out var mc))
        {
            ;
        }
        else if (messageCenterStates.TryGetValue(guid, out var mcState))
        {
            return "MESSAGECENTER STATE: " + messageCenter[mcState.Item1.id].name + " " + mcState.Item1.name + " " + mcState.Item2;
        }

        Program.Log(LogLevel.Information, "Unknown id received: " + GuidConverter.GuidToString(guid));
        return null;
        //throw new Exception("Guid not found: " + GuidConverter.GuidToString(guid));
    }

    private bool StateReceived(Control control, string ctrlStateName, object value, out LoxoneDevice? device)
    {
        while (!WebhookProcessor.Initialized)
        {
            Thread.Sleep(100);
        }

        if (WebhookProcessor.Devices.TryGet(control, out device))
        {
            Program.Log(LogLevel.Debug, $"{device.Id} new value: {value} ({value.GetType()})");
            device.States[ctrlStateName] = value;

            if (device.StatesToUpdate.Contains(ctrlStateName))
            {
                WebhookProcessor.UpdateState(device);
            }
        }
        else
        {
            WebhookProcessor.UnknownDevice.States[ctrlStateName] = value;
        }

        if (control.type == "Meter" || control.type == "EFM")
        {
            return false;
        }

        return true;
    }
}