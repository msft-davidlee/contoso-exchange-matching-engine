using Demo.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Demo.MatchingEngine
{
    public class MulticastService
    {
        private readonly string _multicastIPAddress;
        private readonly string _multicastPort;
        private readonly ILogger<MulticastService> _logger;

        public MulticastService(IConfiguration configuration, ILogger<MulticastService> logger)
        {
            _multicastIPAddress = configuration["MulticastIPAddress"];
            _multicastPort = configuration["MulticastPort"];
            _logger = logger;
        }

        public static void SendTest(
            string multicastIPAddress, string multicastPort, string testData)
        {
            if (!string.IsNullOrEmpty(multicastIPAddress) &&
                !string.IsNullOrEmpty(multicastPort))
            {
                IPAddress mcastAddress;
                int mcastPort;
                if (IPAddress.TryParse(multicastIPAddress, out mcastAddress) &&
                    int.TryParse(multicastPort, out mcastPort))
                {
                    using (var udpClient = new UdpClient(AddressFamily.InterNetwork))
                    {
                        var ipEndPoint = new IPEndPoint(mcastAddress, mcastPort);
                        var data = Encoding.Default.GetBytes(testData);
                        udpClient.JoinMulticastGroup(mcastAddress);
                        udpClient.Send(data, data.Length, ipEndPoint);
                        udpClient.Close();
                    }
                }
                else
                {
                    throw new ApplicationException("Invalid multicastIPAddress or multicastPort!");
                }
            }
            else
            {
                throw new ApplicationException("No multicastIPAddress or multicastPort provided!");
            }
        }

        public void Send(MarketDataReport marketDataReport)
        {
            if (!string.IsNullOrEmpty(_multicastIPAddress) &&
                !string.IsNullOrEmpty(_multicastPort))
            {
                IPAddress mcastAddress;
                int mcastPort;
                if (IPAddress.TryParse(_multicastIPAddress, out mcastAddress) &&
                    int.TryParse(_multicastPort, out mcastPort))
                {
                    try
                    {
                        using (var udpClient = new UdpClient(AddressFamily.InterNetwork))
                        {
                            var ipEndPoint = new IPEndPoint(mcastAddress, mcastPort);
                            var data = marketDataReport.GetBytes();

                            _logger.LogInformation($"Broadcasting message={marketDataReport} ip={_multicastIPAddress} port={_multicastPort}");

                            udpClient.JoinMulticastGroup(mcastAddress);
                            udpClient.Send(data, data.Length, ipEndPoint);
                            udpClient.Close();
                        }
                    }
                    catch (SocketException se)
                    {
                        var seMessage = se.Message;
                        _logger.LogError($"SocketException {seMessage}", se);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e.Message, e);
                    }
                }
                else
                {
                    _logger.LogInformation("Unable to send message to multicast endpoint as configuration is not valid.");
                }
            }
            else
            {
                _logger.LogInformation("Unable to send message to multicast endpoint as it is not configured.");
            }
        }
    }
}