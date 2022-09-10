using System.Xml.Serialization;

namespace BF2WebAdmin.Shared;

[XmlRoot(ElementName="banlist")]
public class Banlist
{
    [XmlElement(ElementName = "ban")] 
    public List<Ban>? Bans { get; set; } = new();
}

[XmlRoot(ElementName="ban")]
public class Ban {
    [XmlElement(ElementName="datetime")]
    public string? Datetime { get; set; }
    [XmlElement(ElementName="nick")]
    public string? Nick { get; set; }
    [XmlElement(ElementName="method")]
    public string? Method { get; set; } // Key or Address
    [XmlElement(ElementName="period")]
    public string? Period { get; set; }
    [XmlElement(ElementName="address")]
    public string? Address { get; set; }
    [XmlElement(ElementName="cdkeyhash")]
    public string? Cdkeyhash { get; set; }
    [XmlElement(ElementName="profileid")]
    public string? Profileid { get; set; }
    [XmlElement(ElementName="by")]
    public string? By { get; set; }
    [XmlElement(ElementName="reason")]
    public string? Reason { get; set; }
}
