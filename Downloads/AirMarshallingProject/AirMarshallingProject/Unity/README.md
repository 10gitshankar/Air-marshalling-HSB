# Unity Scripts

## Files

- `GestureReceiver.cs` — listens on UDP port `4210`, combines LEFT/RIGHT
  packets from both ESP32 boards into one gesture. Attach to an empty
  GameObject named `GestureManager`.
- `PlaneController.cs` — attach to the plane GameObject. Reads gestures from
  `GestureReceiver` every frame and moves/rotates the plane accordingly.
- `DualHandMonitor.cs` *(add here if not already present)* — standalone
  diagnostic tool on port 4210. **Disable `GestureReceiver.cs` first if using
  this**, or you'll get a port conflict.

## Setup — do this or the plane won't move

1. Add `GestureReceiver.cs` to a GameObject (e.g. `GestureManager`).
2. Add `PlaneController.cs` to your plane GameObject.
3. **In the Inspector, drag the `GestureManager` GameObject into the
   `Receiver` field on `PlaneController`.** If this is left unassigned
   (null), `Update()` exits every frame and the plane will never respond —
   this has bitten us before, double-check it.
4. Confirm `port = 4210` matches the `unityPort` set in both `.ino` files.

## Debugging

- `PlaneController.rawDebugStatus` — visible in the Inspector while in Play
  mode, shows live `LEFT: ... RIGHT: ... Gesture: ...` state.
- `HOLD` from either hand is already collapsed to `STOP` inside
  `GestureReceiver.GetGesture()` — no extra handling needed in
  `PlaneController`.

## Testing without ESP32 hardware

Run `Tools/udp_gesture_simulator.py` from a terminal after pressing Play —
it sends fake packets in the correct format so you can test all four
gestures plus the HOLD freeze behavior without any hardware connected.
