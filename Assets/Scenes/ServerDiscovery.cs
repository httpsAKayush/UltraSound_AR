using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class ServerDiscovery : MonoBehaviour
{
    [Header("Discovery Settings")]
    public string serviceName = "ultrasound";   // or "ct_pipeline" for the other instance
    public int broadcastPort = 5003;            // must match the Python broadcaster's port

    public string DiscoveredIP { get; private set; }
    public Dictionary<string, int> DiscoveredPorts { get; private set; } = new Dictionary<string, int>();
    public bool IsDiscovered => !string.IsNullOrEmpty(DiscoveredIP);

    private UdpClient discoveryClient;
    private Thread discoveryThread;
    private volatile bool running = false;
    private readonly object lockObj = new object();

    void Awake()
    {
        StartDiscovery();
    }

    void OnDestroy()
    {
        running = false;
        discoveryClient?.Close();
    }

    private void StartDiscovery()
    {
        running = true;
        discoveryThread = new Thread(Loop);
        discoveryThread.IsBackground = true;
        discoveryThread.Start();
    }

    private void Loop()
    {
        const string MulticastGroup = "239.255.42.42";
        try
        {
            discoveryClient = new UdpClient();
            discoveryClient.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress, true);
            discoveryClient.Client.Bind(
                new IPEndPoint(IPAddress.Any, broadcastPort));

            // Join multicast group
            discoveryClient.JoinMulticastGroup(
                IPAddress.Parse(MulticastGroup));

            discoveryClient.Client.ReceiveTimeout = 5000;

            while (running)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = discoveryClient.Receive(ref remoteEP);
                    string json = Encoding.UTF8.GetString(data);
                    Debug.Log($"[ServerDiscovery] Received: {json}");

                    var msg = JsonUtility.FromJson<BroadcastMessage>(json);
                    if (msg == null || msg.service != serviceName) continue;

                    var ports = ParsePorts(json);
                    lock (lockObj)
                    {
                        DiscoveredIP    = msg.ip;
                        DiscoveredPorts = ports;
                    }
                    Debug.Log($"[ServerDiscovery] Discovered {serviceName} at {DiscoveredIP}");
                    // // Stop listening once discovered
                    // running = false;
                    // break;
                }
                catch (SocketException)
                {
                    // timeout, loop again
                }
            }

            discoveryClient.DropMulticastGroup(IPAddress.Parse(MulticastGroup));
        }
        catch (Exception e)
        {
            Debug.LogError($"ServerDiscovery [{serviceName}] error: {e.Message}");
        }
        finally
        {
            discoveryClient?.Close();
        }
    }

    private Dictionary<string, int> ParsePorts(string json)
    {
        var result = new Dictionary<string, int>();
        int idx = json.IndexOf("\"ports\"");
        if (idx < 0) return result;

        int braceStart = json.IndexOf('{', idx);
        int braceEnd   = json.IndexOf('}', braceStart);
        if (braceStart < 0 || braceEnd < 0) return result;

        string portsBlock = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
        string[] pairs = portsBlock.Split(',');
        foreach (var pair in pairs)
        {
            string[] kv = pair.Split(':');
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().Trim('"');
            if (int.TryParse(kv[1].Trim(), out int val))
            {
                result[key] = val;
            }
        }
        return result;
    }

    public int GetPort(string portKey)
    {
        lock (lockObj)
        {
            return DiscoveredPorts.TryGetValue(portKey, out int port) ? port : -1;
        }
    }

    [Serializable]
    private class BroadcastMessage
    {
        public string service;
        public string ip;
    }
}