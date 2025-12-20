using System.Net.NetworkInformation;

namespace tunnelx.Models
{
    public class InterfaceModel
    {
        public string description { get; set; }
        public string name { get; set; }
        public NetworkInterfaceType networkInterfaceType { get; set; }
        public OperationalStatus operationalStatus { get; set; }
        public long speed { get; set; }
        public PhysicalAddress physicalAddress { get; set; }
        public IPv4InterfaceStatistics ipv4InterfaceStatistics { get; set; }
        public IPInterfaceStatistics ipInterfaceStatistics { get; set; }
        public IPInterfaceProperties ipInterfaceProperties { get; set; }
    }
}