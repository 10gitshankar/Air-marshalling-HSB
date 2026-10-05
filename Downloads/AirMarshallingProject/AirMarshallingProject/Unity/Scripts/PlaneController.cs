/*
  Air Marshalling Project — PlaneController.cs
  Attach this to your Airplane GameObject on the runway.
  Drag the GameObject containing "GestureReceiver" into the "Receiver" field.
*/

using UnityEngine;

public class PlaneController : MonoBehaviour
{
    [Header("Link to Gesture Receiver")]
    public GestureReceiver receiver;

    [Header("Movement Settings")]
    public float moveSpeed = 3.0f;
    public float turnSpeed = 35.0f;
    public float slowSpeedFactor = 0.4f;

    [Header("Live Status (Read-Only)")]
    public string currentGesture = "STOP";
    public bool engineOn = true;
    public bool chocksInserted = false;

    [Header("HUD Display")]
    public bool showHUD = true;

    void Update()
    {
        if (receiver == null) return;

        // GestureReceiver बाट हालको आधिकारिक सिग्नल लिने
        currentGesture = receiver.GetGesture();

        // १. इन्जिन र चक्सको अवस्था व्यवस्थापन
        switch (currentGesture)
        {
            case "START_ENGINE":
                engineOn = true;
                break;
            case "CUT_ENGINES":
                engineOn = false;
                break;
            case "CHOCKS":
                // Chocks toggle गर्न सकिन्छ
                chocksInserted = true;
                break;
            case "ALL_CLEAR":
                chocksInserted = false;
                break;
        }

        // यदि चक्स हालिएको छ वा इन्जिन बन्द छ भने प्लेन हिँड्दैन
        if (chocksInserted || !engineOn)
        {
            return;
        }

        // २. प्लेनको गति र मोशन
        switch (currentGesture)
        {
            case "MOVE_AHEAD":
                transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
                break;

            case "SLOW_DOWN":
                transform.Translate(Vector3.forward * (moveSpeed * slowSpeedFactor) * Time.deltaTime);
                break;

            case "TURN_LEFT":
                transform.Rotate(Vector3.up, -turnSpeed * Time.deltaTime);
                break;

            case "TURN_RIGHT":
                transform.Rotate(Vector3.up, turnSpeed * Time.deltaTime);
                break;

            case "NORMAL_STOP":
            case "EMERGENCY_STOP":
            case "HOLD_POSITION":
            case "STOP":
            default:
                // प्लेन रोकिन्छ (Still)
                break;
        }
    }

    // स्क्रिनमा सिधै लाइभ स्टाटस देखाउने GUI
    void OnGUI()
    {
        if (!showHUD) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = (currentGesture == "EMERGENCY_STOP" || currentGesture == "STOP") ? Color.red : Color.green;

        GUI.Box(new Rect(15, 15, 480, 110), "AIR MARSHALLING SIMULATION");
        GUI.Label(new Rect(25, 45, 450, 40), "SIGNAL: " + currentGesture, style);
        
        GUIStyle subStyle = new GUIStyle(GUI.skin.label);
        subStyle.fontSize = 14;
        subStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(25, 80, 450, 30), 
            string.Format("Engine: {0} | Chocks: {1} | MoveSpeed: {2}", 
            engineOn ? "RUNNING" : "OFF", chocksInserted ? "INSERTED" : "REMOVED", currentGesture == "MOVE_AHEAD" ? moveSpeed : 0), subStyle);
    }
}
