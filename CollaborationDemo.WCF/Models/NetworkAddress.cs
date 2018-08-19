using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace CollaborationDemo.WCF.Models
{
    public class NetworkAddress
    {
        public static NetworkAddress Parse(string networkAddress)
        {
            return new NetworkAddress(networkAddress);
        }

        private NetworkAddress(string networkAddress)
        {
            if (string.IsNullOrWhiteSpace(networkAddress))
            {
                throw new ArgumentException("NetworkAddress string representation cannot be empty");
            }

            var parts = networkAddress.Split('/');
            SignificantBits = int.Parse(parts[1]);
            var originalAddress = IPAddress.Parse(parts[0]);
            Address = SanitizeNetworkAddress(originalAddress, SignificantBits);
            BroadcastAddress = CalculateBroadcastAddress(originalAddress, SignificantBits);
        }

        private static IPAddress SanitizeNetworkAddress(IPAddress address, int significantBits)
        {
            var bytes = address.GetAddressBytes();
            var allBits = bytes.Length * 8;
            for (var i = significantBits; i < allBits; i++)
            {
                int index = i / 8;
                int position = i % 8;
                byte mask = Convert.ToByte(1 << (7-position));
                bytes[index] = Convert.ToByte(bytes[index] & ~mask);
            }
            return new IPAddress(bytes);
        }

        private static IPAddress CalculateBroadcastAddress(IPAddress address, int significantBits)
        {
            var bytes = address.GetAddressBytes();
            var allBits = bytes.Length * 8;
            for (var i = significantBits; i < allBits; i++)
            {
                int index = i / 8;
                int position = i % 8;
                byte mask = Convert.ToByte(1 << (7 - position));
                bytes[index] = Convert.ToByte(bytes[index] | mask);
            }
            return new IPAddress(bytes);
        }

        public IPAddress Address { get; }
        public int SignificantBits { get; }
        public IPAddress BroadcastAddress { get; }

        public bool OwnsIPAddress(IPAddress unicastAddress)
        {
            var addressBytes = unicastAddress.GetAddressBytes();
            var networkBytes = Address.GetAddressBytes();
            bool matches = true;
            for (var i = 0; i < SignificantBits; i++)
            {
                var index = i / 8;
                var position = i % 8;
                var mask = 1 << (8 - position);
                if ((addressBytes[index] & mask) != (networkBytes[index] & mask))
                {
                    matches = false;
                    break;
                }
            }
            return matches;
        }

        public IPAddress GetLocalAddress()
        {
            var addressInfo = NetworkInterface
                .GetAllNetworkInterfaces()
                .Select(iface => iface.GetIPProperties())
                .SelectMany(props => props.UnicastAddresses)
                .Where(addr => this.OwnsIPAddress(addr.Address))
                .First();

            return addressInfo.Address;
        }

        public override string ToString()
        {
            return $"{Address}/{SignificantBits}";
        }
    }
}
