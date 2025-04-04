using System.Collections;
using LoxoneNet.Loxone;
using System.Diagnostics.CodeAnalysis;

namespace LoxoneNet;

internal class DeviceList : IEnumerable<LoxoneDevice>
{
    private readonly Dictionary<string, LoxoneDevice> _byId = new Dictionary<string, LoxoneDevice>();
    
    private readonly Dictionary<Control, LoxoneDevice> _byControl = new Dictionary<Control, LoxoneDevice>();

    public bool TryGet(Control control, [MaybeNullWhen(false)] out LoxoneDevice device)
    {
        return _byControl.TryGetValue(control, out device);
    }

    public bool TryGet(string id, [MaybeNullWhen(false)] out LoxoneDevice device)
    {
        return _byId.TryGetValue(id, out device);
    }

    public void Add(LoxoneDevice device)
    {
        _byId.Add(device.Id, device);
        _byControl.Add(device.Control, device);
    }

    public void Clear()
    {
        _byId.Clear();
        _byControl.Clear();
    }

    public IEnumerator<LoxoneDevice> GetEnumerator()
    {
        return _byId.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}