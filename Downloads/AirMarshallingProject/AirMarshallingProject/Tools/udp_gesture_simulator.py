"""
udp_gesture_simulator.py

Fakes the UDP packets that the ESP32 boards normally send, so GestureReceiver.cs
can be tested in Unity WITHOUT any hardware connected.

Packet format (must match esp32_left/right_with_limit_sw.ino exactly):
    "HAND,STATE,gyroMagnitude"
    HAND  = LEFT or RIGHT
    STATE = MOVING, STILL, or HOLD
    gyroMagnitude = any float (not used by Unity logic, just logged)

USAGE:
    1. Press Play in Unity first (GestureReceiver.cs must be listening on port 4210).
    2. Run:  python udp_gesture_simulator.py
    3. Pick a gesture number and press Enter. It sends packets for BOTH hands
       every 0.3s (matching roughly how fast the real ESP32 boards report)
       until you pick a different gesture or quit.

No external libraries needed — uses only Python's built-in socket module.
"""

import socket
import time
import threading

UNITY_IP = "127.0.0.1"   # change to Unity machine's LAN IP if running on another PC
UNITY_PORT = 4210
SEND_INTERVAL = 0.3       # seconds between packets, mimics real board send rate

GESTURES = {
    "1": ("STOP",       "STILL",  "STILL"),
    "2": ("MOVE_AHEAD",  "MOVING", "MOVING"),
    "3": ("TURN_LEFT",   "MOVING", "STILL"),
    "4": ("TURN_RIGHT",  "STILL",  "MOVING"),
    "5": ("HOLD (left)", "HOLD",   "STILL"),
    "6": ("HOLD (right)","STILL",  "HOLD"),
}

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
stop_flag = threading.Event()
current_lock = threading.Lock()
current_states = ("STILL", "STILL")


def sender_loop():
    while not stop_flag.is_set():
        with current_lock:
            left_state, right_state = current_states
        try:
            sock.sendto(f"LEFT,{left_state},50.0".encode("ascii"), (UNITY_IP, UNITY_PORT))
            sock.sendto(f"RIGHT,{right_state},50.0".encode("ascii"), (UNITY_IP, UNITY_PORT))
        except OSError as e:
            print(f"Send error: {e}")
        time.sleep(SEND_INTERVAL)


def main():
    global current_states
    print(f"Sending fake gesture packets to {UNITY_IP}:{UNITY_PORT}")
    print("Make sure you pressed Play in Unity BEFORE choosing a gesture below.\n")

    t = threading.Thread(target=sender_loop, daemon=True)
    t.start()

    while True:
        print("\nChoose a gesture to simulate:")
        for key, (label, _, _) in GESTURES.items():
            print(f"  {key}. {label}")
        print("  q. Quit")

        choice = input("> ").strip().lower()
        if choice == "q":
            stop_flag.set()
            sock.close()
            print("Stopped.")
            break

        if choice in GESTURES:
            label, l_state, r_state = GESTURES[choice]
            with current_lock:
                current_states = (l_state, r_state)
            print(f"Now continuously sending: LEFT={l_state}  RIGHT={r_state}  (expect plane: {label})")
        else:
            print("Invalid choice, try again.")


if __name__ == "__main__":
    main()
