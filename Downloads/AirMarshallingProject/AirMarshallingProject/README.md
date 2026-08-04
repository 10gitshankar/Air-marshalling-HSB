# Air Marshalling Simulation Project

Virtual air marshalling training system. Two ESP32 + MPU6050 hand units send
gesture data over WiFi (UDP) to a Unity scene, where a plane responds to the
detected marshalling signal.

## Gestures

| Left hand | Right hand | Gesture       |
|-----------|------------|---------------|
| Still     | Still      | Stop / Attention |
| Moving    | Moving     | Move Ahead    |
| Still     | Moving     | Turn Right    |
| Moving    | Still      | Turn Left     |

If either hand's limit switch is not pressed (`HOLD` state), the plane always
freezes (`STOP`), regardless of the other hand.

## Folder structure

- `Firmware/` — Arduino code for both ESP32 hand units
- `Unity/` — C# scripts for the Unity simulation
- `Hardware/KiCad/` — schematic + PCB design files
- `Hardware/3D_Models/` — enclosure STL files for 3D printing
- `Tools/` — utilities, including a UDP packet simulator for testing the
  Unity side without any hardware connected
- `docs/` — wiring photos, diagrams, demo screenshots

Each folder has its own `README.md` with setup-specific details.

## Shared packet contract (Firmware ↔ Unity)

Both ESP32 boards send UDP packets to the Unity machine on port `4210` in
this exact format:

```
HAND,STATE,gyroMagnitude
```

- `HAND` — `LEFT` or `RIGHT`
- `STATE` — `MOVING`, `STILL`, or `HOLD`
- `gyroMagnitude` — float, logged for debugging only, not used in gesture logic

**Important:** if this format ever changes on the firmware side, update
`Unity/README.md` in the same commit — the two sides must always agree on
this contract or gestures will silently stop working.

## Testing without hardware

See `Tools/udp_gesture_simulator.py` — sends fake packets matching the
format above so the Unity side can be tested standalone.
