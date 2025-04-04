using System.Diagnostics;

namespace LoxoneNet.Loxone;

[DebuggerDisplay("id = {id}, name = {name}, analog = {analog}")]
class TimeInfo
{
    public int id { get; set; }

    public string name { get; set; }

    public bool analog { get; set; }
}