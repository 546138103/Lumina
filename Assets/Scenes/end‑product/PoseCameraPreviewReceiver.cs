using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-1100)]
[DisallowMultipleComponent]
public sealed class PoseCameraPreviewReceiver : MonoBehaviour
{
    private const string PreviewHost = "127.0.0.1";
    private const int PreviewPort = 52734;
    private const int HeaderSize = 4;
    private const int MaxFrameBytes = 4 * 1024 * 1024;

    public Texture2D PreviewTexture { get; private set; }
    public float LastFrameRealtime { get; private set; } = -999f;
    public event Action<Texture2D> FrameUpdated;

    private readonly object frameLock = new object();
    private readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();

    private byte[] latestFrame;
    private Thread receiverThread;
    private TcpListener listener;
    private TcpClient activeClient;
    private volatile bool running;

    private void OnEnable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning(
            "[PoseCameraPreview] 本机 TCP 预览不支持 WebGL Player。",
            this);
        enabled = false;
#else
        StartReceiver();
#endif
    }

    private void Update()
    {
        while (logQueue.TryDequeue(out string message))
        {
            Debug.Log(message, this);
        }

        byte[] frame = TakeLatestFrame();
        if (frame == null)
        {
            return;
        }

        if (PreviewTexture == null)
        {
            PreviewTexture = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false);
            PreviewTexture.name = "Pose Camera Preview";
            PreviewTexture.wrapMode = TextureWrapMode.Clamp;
            PreviewTexture.filterMode = FilterMode.Bilinear;
        }

        // Texture2D and ImageConversion are Unity APIs and must stay on the main thread.
        if (ImageConversion.LoadImage(PreviewTexture, frame, false))
        {
            LastFrameRealtime = Time.unscaledTime;
            FrameUpdated?.Invoke(PreviewTexture);
        }
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();

        if (PreviewTexture != null)
        {
            Destroy(PreviewTexture);
            PreviewTexture = null;
        }
    }

    public bool HasRecentFrame(float timeoutSeconds)
    {
        return PreviewTexture != null &&
            Time.unscaledTime - LastFrameRealtime <= timeoutSeconds;
    }

    private void StartReceiver()
    {
        if (running)
        {
            return;
        }

        running = true;
        receiverThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "Lumina Pose Camera Preview Receiver"
        };
        receiverThread.Start();
    }

    private void StopReceiver()
    {
        running = false;

        try
        {
            activeClient?.Close();
        }
        catch (SocketException)
        {
        }

        try
        {
            listener?.Stop();
        }
        catch (SocketException)
        {
        }

        if (receiverThread != null &&
            receiverThread.IsAlive &&
            Thread.CurrentThread != receiverThread)
        {
            receiverThread.Join(1000);
        }

        receiverThread = null;
        activeClient = null;
        listener = null;

        lock (frameLock)
        {
            latestFrame = null;
        }
    }

    private void ReceiveLoop()
    {
        try
        {
            listener = new TcpListener(
                IPAddress.Parse(PreviewHost),
                PreviewPort);
            listener.Start();
            logQueue.Enqueue(
                $"[PoseCameraPreview] Listening @ {PreviewHost}:{PreviewPort}");

            while (running)
            {
                try
                {
                    using (TcpClient client = listener.AcceptTcpClient())
                    {
                        activeClient = client;
                        client.NoDelay = true;
                        logQueue.Enqueue("[PoseCameraPreview] Python preview connected.");
                        ReceiveFrames(client.GetStream());
                    }
                }
                catch (SocketException)
                {
                    if (running)
                    {
                        logQueue.Enqueue(
                            "[PoseCameraPreview] Preview connection interrupted; waiting for reconnect.");
                    }
                }
                catch (IOException)
                {
                    if (running)
                    {
                        logQueue.Enqueue(
                            "[PoseCameraPreview] Preview stream closed; waiting for reconnect.");
                    }
                }
                finally
                {
                    activeClient = null;
                }
            }
        }
        catch (SocketException exception)
        {
            if (running)
            {
                logQueue.Enqueue(
                    $"[PoseCameraPreview] Cannot listen on port {PreviewPort}: {exception.Message}");
            }
        }
        finally
        {
            try
            {
                listener?.Stop();
            }
            catch (SocketException)
            {
            }
        }
    }

    private void ReceiveFrames(NetworkStream stream)
    {
        byte[] header = new byte[HeaderSize];

        while (running)
        {
            ReadExactly(stream, header, HeaderSize);
            int frameLength =
                (header[0] << 24) |
                (header[1] << 16) |
                (header[2] << 8) |
                header[3];

            if (frameLength <= 0 || frameLength > MaxFrameBytes)
            {
                throw new IOException(
                    $"Invalid preview frame size: {frameLength}");
            }

            byte[] frame = new byte[frameLength];
            ReadExactly(stream, frame, frameLength);

            lock (frameLock)
            {
                // Replacing instead of queueing keeps latency bounded.
                latestFrame = frame;
            }
        }
    }

    private void ReadExactly(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;

        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read <= 0)
            {
                throw new EndOfStreamException();
            }

            offset += read;
        }
    }

    private byte[] TakeLatestFrame()
    {
        lock (frameLock)
        {
            byte[] frame = latestFrame;
            latestFrame = null;
            return frame;
        }
    }
}
