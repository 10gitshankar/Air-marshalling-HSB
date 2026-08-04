/*
  Air Marshalling Project — PlaneController.cs

  Attach this to your plane GameObject.
  Drag the GameObject that has GestureReceiver.cs into the "Receiver" field
  in the Inspector.

  Note on the limit-switch HOLD gate:
  GestureReceiver.GetGesture() already collapses any "HOLD" state (switch not
  pressed on either hand) down to "STOP" before it ever reaches this script.
  So no HOLD-specific case is needed here — the existing "STOP"/default case
  already freezes the plane correctly. This script only needed a way to show
  that clearly during testing, which is what rawDebugStatus below is for.
*/

using UnityEngine;

public class PlaneController : MonoBehaviour
{
    public GestureReceiver receiver;
    public float turnSpeed = 30f;   // degrees per second
    public float moveSpeed = 2f;    // units per second

    [Tooltip("Optional: shows the last gesture applied, visible in Inspector while testing")]
    public string currentGesture = "STOP";

    [Tooltip("Raw LEFT/RIGHT/gesture status from GestureReceiver, for quick Inspector debugging")]
    public string rawDebugStatus = "";

    void Update()
    {
        if (receiver == null) return;

        currentGesture = receiver.GetGesture();
        rawDebugStatus = receiver.GetDebugStatus();

        switch (currentGesture)
        {
            case "TURN_LEFT":
                transform.Rotate(Vector3.up, -turnSpeed * Time.deltaTime);
                break;

            case "TURN_RIGHT":
                transform.Rotate(Vector3.up, turnSpeed * Time.deltaTime);
                break;

            case "MOVE_AHEAD":
                transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);
                break;

            case "STOP":
            default:
                // Plane stays where it is. This also covers the HOLD-gated case:
                // GestureReceiver already turns any HOLD into STOP for us.
                break;
        }
    }
}
