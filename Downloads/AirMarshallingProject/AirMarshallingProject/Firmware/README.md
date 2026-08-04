# Firmware — ESP32 Hand Units

Two boards, one per hand. Firmware is nearly identical between them — the
main difference is the `HAND` constant (`"LEFT"` or `"RIGHT"`).

## Structure

Each `.ino` file MUST stay inside its own folder with a matching folder name,
or the Arduino IDE will fail to open/compile it:

```
esp32_left_with_limit_sw/esp32_left_with_limit_sw.ino
esp32_right_with_limit_sw/esp32_right_with_limit_sw.ino
```

## Key design points

- Uses **raw I2C register access** (no MPU6050 library) — reads register
  `0x3B`, wakes the sensor via `PWR_MGMT_1` at address `0x6B`.
  `GYRO_SENSITIVITY = 131.0` LSB/deg/s.
- I2C pins are the ESP32 defaults set implicitly by `Wire.begin()`
  (GPIO21 = SDA, GPIO22 = SCL). GY-521 XDA/XCL pins are not used.
- Limit switch on **GPIO27** (`INPUT_PULLUP` to GND) gates UDP transmission
  only — the MPU keeps reading continuously. The switch never touches the
  I2C lines directly (mechanical switching on SDA/SCL causes bus hang).
- WiFi status LED on **GPIO2**, non-blocking, with auto-reconnect.
- Sends UDP packets to the Unity machine's IP on port `4210` — see root
  `README.md` for the packet format.

## Power / upload notes

- Powered via 5V pin from a buck converter + 2x Li-ion cells in series.
- **Always disconnect the buck converter before USB upload** — running both
  power sources at once causes upload failures.
- A 1000µF capacitor across 5V/GND is recommended to buffer WiFi current
  spikes.

## Before flashing

Set the Unity machine's IP address and WiFi credentials at the top of each
`.ino` file.
