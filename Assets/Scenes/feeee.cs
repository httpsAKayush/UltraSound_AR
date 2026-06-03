using UnityEngine;
using UnityEngine.UI;
using System;
using System.Net.Sockets;
using System.Threading;

public class feeee : MonoBehaviour
{
    public static feeee Instance;

    [Header("Network")]
    public string serverIP = "172.18.17.187";
    public int serverPort = 5000;

    [Header("Display")]
    public RawImage displayPanel;

    [Header("Status")]
    public bool isConnected = false;

    private TcpClient client;
    private NetworkStream networkStream;
    private Texture2D texture;
    private byte[] latestFrame;
    private bool newFrameAvailable = false;
    private bool running = true;
    private Thread receiveThread;

    // Other scripts can access the live feed texture through this
    public Texture FeedTexture
    {
        get { return texture; }
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);

        if (displayPanel != null)
            displayPanel.texture = texture;

        receiveThread = new Thread(ConnectionLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ConnectionLoop()
    {
        while (running)
        {
            try
            {
                Debug.Log($"[FrameReceiver] Connecting to {serverIP}:{serverPort}...");

                client = new TcpClient();
                client.Connect(serverIP, serverPort);

                networkStream = client.GetStream();
                isConnected = true;

                Debug.Log("[FrameReceiver] Connected to PC server successfully");

                ReceiveLoop();
            }
            catch (Exception e)
            {
                isConnected = false;

                Debug.LogWarning(
                    $"[FrameReceiver] {e.Message} — retrying in 2s..."
                );

                Thread.Sleep(2000);
            }
        }
    }

    void ReceiveLoop()
    {
        byte[] headerBuffer = new byte[4];

        while (running)
        {
            ReadExact(headerBuffer, 4);

            int frameSize =
                (headerBuffer[0] << 24) |
                (headerBuffer[1] << 16) |
                (headerBuffer[2] << 8) |
                headerBuffer[3];

            if (frameSize <= 0 || frameSize > 5000000)
            {
                Debug.LogWarning(
                    $"[FrameReceiver] Bad frame size: {frameSize}"
                );
                continue;
            }

            byte[] frameData = new byte[frameSize];

            ReadExact(frameData, frameSize);

            latestFrame = frameData;
            newFrameAvailable = true;
        }
    }

    void ReadExact(byte[] buffer, int count)
    {
        int totalRead = 0;

        while (totalRead < count)
        {
            int bytesRead = networkStream.Read(
                buffer,
                totalRead,
                count - totalRead
            );

            if (bytesRead == 0)
                throw new Exception("Server closed connection");

            totalRead += bytesRead;
        }
    }

    void Update()
    {
        if (newFrameAvailable && latestFrame != null)
        {
            texture.LoadImage(latestFrame);
            texture.Apply();

            newFrameAvailable = false;
        }
    }

    void OnDestroy()
    {
        running = false;

        try
        {
            networkStream?.Close();
            client?.Close();
        }
        catch
        {
        }
    }
}