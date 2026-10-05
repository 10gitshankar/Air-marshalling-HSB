/*
  Air Marshalling Project — GestureReceiver.cs
  Attach this to an empty GameObject in your scene named "GestureManager".
  Receives UDP packets from both ESP32 wands on port 4210.
*/

using UnityEngine;
using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class GestureReceiver : MonoBehaviour
{
    public enum Elevation { DOWN, MID, UP }
    public enum Motion { STILL, MOVING, FAST }

    [Header("Network Configuration")]
    [Tooltip("Must match unityPort in both ESP32 .ino files")]
    public int port = 4210;
    public float staleTimeoutSeconds = 2f;

    [Header("Elevation Thresholds (Pitch in degrees)")]
    public float upPitch = 45f;
    public float downPitch = -30f;
    public float identifyPitch = 70f;

    [Header("Sensor Invert / Offsets")]
    public bool invertLeftPitch = false;
    public bool invertRightPitch = false;

    [Header("Motion Thresholds")]
    public float fastGyroThreshold = 180f;

    [Header("Debounce / Filter")]
    public float gestureHoldTime = 0.25f;

    [Header("Live Data - Left Hand")]
    public string leftRawState = "HOLD";
    public float leftPitch;
    public float leftRoll;
    public float leftYaw;
    public float leftGyro;
    public Elevation leftElevation = Elevation.MID;
    public Motion leftMotion = Motion.STILL;

    [Header("Live Data - Right Hand")]
    public string rightRawState = "HOLD";
    public float rightPitch;
    public float rightRoll;
    public float rightYaw;
    public float rightGyro;
    public Elevation rightElevation = Elevation.MID;
    public Motion rightMotion = Motion.STILL;

    [Header("Final Detected Gesture")]
    public string stableGesture = "STOP";

    private UdpClient udpClient;
    private Thread receiveThread;
    private readonly object lockObj = new object();

    private double leftLastSeen = -999;
    private double rightLastSeen = -999;

    private float rawL_p, rawL_r, rawL_y, rawL_g;
    private string rawL_s = "HOLD";
    private float rawR_p, rawR_r, rawR_y, rawR_g;
    private string rawR_s = "HOLD";

    private string candidate = "STOP";
    private float candidateTimer = 0f;
    private volatile bool running = true;
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();

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
            Debug.LogError("UDP bind error port " + port + ": " + e.Message);
            return;
        }

        Debug.Log("GestureReceiver listening on UDP port " + port);

        while (running)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = udpClient.Receive(ref anyIP);
                string text = System.Text.Encoding.ASCII.GetString(data);
                string[] parts = text.Split(',');

                if (parts.Length >= 2)
                {
                    string hand = parts[0].Trim();
                    string state = parts[1].Trim();

                    float g = 0f, p = 0f, r = 0f, y = 0f;
                    if (parts.Length >= 3) float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out g);
                    if (parts.Length >= 4) float.TryParse(parts[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out p);
                    if (parts.Length >= 5) float.TryParse(parts[4].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out r);
                    if (parts.Length >= 6) float.TryParse(parts[5].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y);

                    lock (lockObj)
                    {
                        if (hand == "LEFT")
                        {
                            rawL_s = state;
                            rawL_g = g;
                            rawL_p = p;
                            rawL_r = r;
                            rawL_y = y;
                            leftLastSeen = clock.Elapsed.TotalSeconds;
                        }
                        else if (hand == "RIGHT")
                        {
                            rawR_s = state;
                            rawR_g = g;
                            rawR_p = p;
                            rawR_r = r;
                            rawR_y = y;
                            rightLastSeen = clock.Elapsed.TotalSeconds;
                        }
                    }
                }
            }
            catch (SocketException) { break; }
            catch (Exception) { }
        }
    }

    void Update()
    {
        double now = clock.Elapsed.TotalSeconds;
        bool leftAlive = (now - leftLastSeen) < staleTimeoutSeconds;
        bool rightAlive = (now - rightLastSeen) < staleTimeoutSeconds;

        lock (lockObj)
        {
            leftRawState = leftAlive ? rawL_s : "OFFLINE";
            rightRawState = rightAlive ? rawR_s : "OFFLINE";

            leftGyro = rawL_g;
            leftPitch = invertLeftPitch ? -rawL_p : rawL_p;
            leftRoll = rawL_r;
            leftYaw = rawL_y;

            rightGyro = rawR_g;
            rightPitch = invertRightPitch ? -rawR_p : rawR_p;
            rightRoll = rawR_r;
            rightYaw = rawR_y;
        }

        leftElevation = ToElevation(leftPitch);
        rightElevation = ToElevation(rightPitch);
        leftMotion = ToMotion(leftRawState, leftGyro);
        rightMotion = ToMotion(rightRawState, rightGyro);

        // Safety Gate: Either button released or disconnected -> Freeze plane
        if (leftRawState == "HOLD" || rightRawState == "HOLD" || !leftAlive || !rightAlive)
        {
            stableGesture = "STOP";
            candidate = "STOP";
            candidateTimer = 0f;
            return;
        }

        string rawGesture = Classify();

        // Debounce Filter
        if (rawGesture == candidate)
        {
            candidateTimer += Time.deltaTime;
            if (candidateTimer >= gestureHoldTime)
            {
                stableGesture = candidate;
            }
        }
        else
        {
            candidate = rawGesture;
            candidateTimer = 0f;
        }
    }

    Elevation ToElevation(float pitchVal)
    {
        if (pitchVal > upPitch) return Elevation.UP;
        if (pitchVal < downPitch) return Elevation.DOWN;
        return Elevation.MID;
    }

    Motion ToMotion(string state, float gyro)
    {
        if (state != "MOVING") return Motion.STILL;
        if (gyro > fastGyroThreshold) return Motion.FAST;
        return Motion.MOVING;
    }

    string Classify()
    {
        bool lM = leftMotion != Motion.STILL;
        bool rM = rightMotion != Motion.STILL;
        bool lS = !lM;
        bool rS = !rM;

        Elevation L = leftElevation;
        Elevation R = rightElevation;

        // 1. Both Hands UP (Overhead)
        if (L == Elevation.UP && R == Elevation.UP)
        {
            if (leftMotion == Motion.FAST || rightMotion == Motion.FAST) return "EMERGENCY_STOP";
            if (lM && rM) return "CHOCKS";
            if (lS && rS)
            {
                if (leftPitch > identifyPitch && rightPitch > identifyPitch) return "IDENTIFY_GATE";
                return "NORMAL_STOP";
            }
        }

        // 2. Engine Start (Left UP still, Right UP moving)
        if (L == Elevation.UP && lS && R == Elevation.UP && rM) return "START_ENGINE";

        // 3. All Clear Salute (Left DOWN still, Right UP still)
        if (L == Elevation.DOWN && lS && R == Elevation.UP && rS) return "ALL_CLEAR";

        // 4. Both Hands DOWN (Low level)
        if (L == Elevation.DOWN && R == Elevation.DOWN)
        {
            if (lM && rM) return "SLOW_DOWN";
            if (lS && rS) return "HOLD_POSITION";
        }

        // 5. Cut Engines (Left DOWN still, Right moving)
        if (L == Elevation.DOWN && lS && rM) return "CUT_ENGINES";

        // 6. Chest Level (MID)
        if (lM && rM) return "MOVE_AHEAD";
        if (lM && rS) return "TURN_LEFT";
        if (lS && rM) return rightMotion == Motion.FAST ? "ENGINE_FIRE" : "TURN_RIGHT";

        return "STOP";
    }

    public string GetGesture()
    {
        return stableGesture;
    }

    public string GetDebugStatus()
    {
        return string.Format("L:[{0} P:{1:F0}° G:{2:F0}]  R:[{3} P:{4:F0}° G:{5:F0}]  GESTURE:{6}",
            leftRawState, leftPitch, leftGyro, rightRawState, rightPitch, rightGyro, stableGesture);
    }

    void OnApplicationQuit() { Shutdown(); }
    void OnDestroy() { Shutdown(); }

    void Shutdown()
    {
        running = false;
        try { udpClient?.Close(); } catch { }
        try { receiveThread?.Join(200); } catch { }
    }
}
