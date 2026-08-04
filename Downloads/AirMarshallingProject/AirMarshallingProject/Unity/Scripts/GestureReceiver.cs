/*
  Air Marshalling Project — GestureReceiver.cs

  Attach this to an empty GameObject in your scene (name it "GestureManager").

  Listens for UDP packets from BOTH ESP32 boards on the same port and
  combines their LEFT/RIGHT state into one gesture that PlaneController.cs
  (and the debug UI) can read.

  Expected packet format from ESP32: "HAND,STATE,gyroMagnitude"
  Example: "LEFT,MOVING,84.2"   or   "RIGHT,STILL,12.1"   or   "LEFT,HOLD,0.0"

  STATE can be:
    MOVING - gyro magnitude above threshold
    STILL  - gyro magnitude below threshold
    HOLD   - limit switch on that hand is NOT pressed. The MPU is still being
             read on the ESP32, but the board is deliberately withholding real
             motion data. Unity must treat HOLD as "freeze the plane" so idle
             sensor jitter never leaks into the simulation while the operator
             isn't actively holding the switch down.

  This version is hardened against common Unity/UDP problems:
   - Handles "port already in use" cleanly (stop Play, it releases the port)
   - Tracks last-received time per hand so you can tell if a board has
     gone silent (WiFi dropped, board powered off, etc.)
   - Thread-safe reads via lock()
*/

using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class GestureReceiver : MonoBehaviour
{
    [Tooltip("Must match unityPort in both ESP32 .ino files")]
    public int port = 4210;

    [Tooltip("If no packet received from a hand for this many seconds, treat it as disconnected")]
    public float staleTimeoutSeconds = 2f;

    private UdpClient udpClient;
    private Thread receiveThread;
    private readonly object lockObj = new object();

    private string leftState = "STILL";
    private string rightState = "STILL";
    private double leftLastSeen = -999;
    private double rightLastSeen = -999;

    private volatile bool running = true;
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();   // thread-safe timer, unlike UnityEngine.Time

    void Start()
    {
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    void ReceiveData()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.ExclusiveAddressUse = false;
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
        }
        catch (Exception e)
        {
            Debug.LogError("Could not bind UDP port " + port + ": " + e.Message +
                            "  (Is another instance already running? Stop Play and try again.)");
            return;
        }

        Debug.Log("GestureReceiver listening on UDP port " + port);

        while (running)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref anyIP);   // blocks until a packet arrives
                string text = System.Text.Encoding.ASCII.GetString(data);
                string[] parts = text.Split(',');

                if (parts.Length >= 2)
                {
                    string hand = parts[0].Trim();
                    string state = parts[1].Trim();

                    lock (lockObj)
                    {
                        if (hand == "LEFT")
                        {
                            leftState = state;
                            leftLastSeen = clock.Elapsed.TotalSeconds;
                        }
                        else if (hand == "RIGHT")
                        {
                            rightState = state;
                            rightLastSeen = clock.Elapsed.TotalSeconds;
                        }
                    }
                }
            }
            catch (SocketException)
            {
                // thrown when the socket is closed on stop — expected, ignore
                break;
            }
            catch (Exception e)
            {
                Debug.LogError("UDP receive error: " + e);
            }
        }
    }

    // Called every frame by PlaneController to decide what the plane should do
    public string GetGesture()
    {
        lock (lockObj)
        {
            bool leftAlive = (clock.Elapsed.TotalSeconds - leftLastSeen) < staleTimeoutSeconds;
            bool rightAlive = (clock.Elapsed.TotalSeconds - rightLastSeen) < staleTimeoutSeconds;

            // A hand that has gone stale (board offline/WiFi dropped) is treated as STILL,
            // same as before — this is unrelated to the HOLD gate below.
            string l = leftAlive ? leftState : "STILL";
            string r = rightAlive ? rightState : "STILL";

            // HOLD gate: if either hand's limit switch is not pressed, force the plane
            // to freeze regardless of what the other hand is doing. This is deliberately
            // checked first, before the MOVING/STILL combination logic, so a HOLD on
            // either board always wins.
            if (l == "HOLD" || r == "HOLD")
            {
                return "STOP";
            }

            if (l == "STILL" && r == "STILL") return "STOP";
            if (l == "MOVING" && r == "STILL") return "TURN_LEFT";
            if (l == "STILL" && r == "MOVING") return "TURN_RIGHT";
            if (l == "MOVING" && r == "MOVING") return "MOVE_AHEAD";
            return "STOP";
        }
    }

    // Useful for an on-screen debug label — shows connection status too
    public string GetDebugStatus()
    {
        lock (lockObj)
        {
            bool leftAlive = (clock.Elapsed.TotalSeconds - leftLastSeen) < staleTimeoutSeconds;
            bool rightAlive = (clock.Elapsed.TotalSeconds - rightLastSeen) < staleTimeoutSeconds;

            string l = leftAlive ? leftState : "OFFLINE";
            string r = rightAlive ? rightState : "OFFLINE";

            return "LEFT: " + l + "   RIGHT: " + r + "   Gesture: " + GetGesture();
        }
    }

    void OnApplicationQuit()
    {
        Shutdown();
    }

    void OnDestroy()
    {
        Shutdown();
    }

    void Shutdown()
    {
        running = false;
        try { udpClient?.Close(); } catch { }
        try { receiveThread?.Join(200); } catch { }
    }
}
