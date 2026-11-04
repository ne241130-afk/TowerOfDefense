using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class MultiplayerLanDiscovery : MonoBehaviour
{
    [Header("Discovery")]
    public int discoveryPort = 47777;
    public int gamePort = 7777;

    private const string DiscoverToken = "TDEF_DISCOVER_V1";
    private const string OfferPrefix = "TDEF_OFFER_V1|";

    private CancellationTokenSource hostCts;
    private Task hostTask;

    public bool IsHostingDiscovery => hostTask != null && !hostTask.IsCompleted;

    public void StartHostResponder()
    {
        StopHostResponder();
        hostCts = new CancellationTokenSource();
        hostTask = Task.Run(() => HostLoop(hostCts.Token), hostCts.Token);
    }

    public void StopHostResponder()
    {
        if (hostCts == null) return;

        try
        {
            hostCts.Cancel();
        }
        catch
        {
        }

        hostCts.Dispose();
        hostCts = null;
        hostTask = null;
    }

    public async Task<string> DiscoverHostAsync(float timeoutSeconds, CancellationToken externalToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(Mathf.Max(0.5f, timeoutSeconds)));
        CancellationToken token = linkedCts.Token;

        using UdpClient client = new UdpClient(0);
        client.EnableBroadcast = true;
        client.Client.ReceiveTimeout = 250;

        byte[] probe = Encoding.UTF8.GetBytes(DiscoverToken);
        IPEndPoint broadcast = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

        Stopwatch sw = Stopwatch.StartNew();
        while (!token.IsCancellationRequested)
        {
            await client.SendAsync(probe, probe.Length, broadcast);

            var waitUntil = sw.Elapsed + TimeSpan.FromMilliseconds(350);
            while (sw.Elapsed < waitUntil && !token.IsCancellationRequested)
            {
                Task<UdpReceiveResult> recvTask = client.ReceiveAsync();
                Task timeoutTask = Task.Delay(120, token);
                Task completed = await Task.WhenAny(recvTask, timeoutTask);

                if (completed != recvTask) continue;

                UdpReceiveResult received = recvTask.Result;
                string text = Encoding.UTF8.GetString(received.Buffer);
                if (!text.StartsWith(OfferPrefix, StringComparison.Ordinal)) continue;

                string[] parts = text.Split('|');
                if (parts.Length < 2) continue;

                string ip = parts[1];
                if (string.IsNullOrWhiteSpace(ip))
                {
                    ip = received.RemoteEndPoint.Address.ToString();
                }

                if (parts.Length >= 3 && int.TryParse(parts[2], out int parsedPort))
                {
                    gamePort = parsedPort;
                }

                return ip;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        StopHostResponder();
    }

    private void HostLoop(CancellationToken token)
    {
        using UdpClient server = new UdpClient(discoveryPort);
        server.EnableBroadcast = true;
        server.Client.ReceiveTimeout = 250;

        while (!token.IsCancellationRequested)
        {
            try
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] bytes = server.Receive(ref remote);
                string text = Encoding.UTF8.GetString(bytes);

                if (!string.Equals(text, DiscoverToken, StringComparison.Ordinal))
                {
                    continue;
                }

                string localIp = GetBestLocalIPv4();
                string payload = OfferPrefix + localIp + "|" + gamePort;
                byte[] response = Encoding.UTF8.GetBytes(payload);
                server.Send(response, response.Length, remote);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut ||
                    ex.SocketErrorCode == SocketError.WouldBlock ||
                    ex.SocketErrorCode == SocketError.Interrupted)
                {
                    continue;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch
            {
                if (token.IsCancellationRequested) return;
            }
        }
    }

    private static string GetBestLocalIPv4()
    {
        try
        {
            IPAddress[] list = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
            foreach (IPAddress ip in list)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch
        {
        }

        return "127.0.0.1";
    }
}
