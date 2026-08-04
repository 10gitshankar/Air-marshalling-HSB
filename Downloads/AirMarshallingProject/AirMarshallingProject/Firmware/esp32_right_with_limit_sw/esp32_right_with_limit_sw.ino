/*
  Air Marshalling Project — RIGHT HAND ESP32 firmware
  Identical to the LEFT firmware except HAND = "RIGHT".
  Uses RAW I2C register access — no MPU6050 library at all.
  This removes any library/platform compatibility issues entirely.

  Wiring (ESP32 <-> MPU6050):
    3.3V -> VCC
    GND  -> GND
    GPIO22 -> SCL
    GPIO21 -> SDA

  WiFi Status LED:
    GPIO2 -> LED (+) long leg (anode)
    LED (-) short leg (cathode) -> 220-330 ohm resistor -> GND
    (Many ESP32 DevKit boards have a built-in LED on GPIO2 already)

    LED behavior:
      - Solid ON      -> WiFi connected
      - Blinking      -> WiFi trying to connect / disconnected

  Data-Gate Limit Switch (does NOT touch I2C/SDA/SCL lines):
    GPIO27 -> one leg of limit switch
    Other leg of limit switch -> GND
    (Uses internal pull-up, so pin reads HIGH when NOT pressed, LOW when pressed)

    Behavior:
      - MPU6050 is still read continuously every loop, regardless of switch state.
      - Switch NOT pressed -> UDP packet sent with state "HOLD" (Unity should freeze
        the plane's position/rotation on HOLD instead of updating it).
      - Switch pressed     -> UDP packet sent with real MOVING/STILL + gyroMag,
        exactly as before.
      - This keeps the I2C bus completely stable; only the software decision of
        what to transmit is gated by the switch.
*/

#include <WiFi.h>
#include <WiFiUdp.h>
#include <Wire.h>

// ---------- EDIT THESE ----------
const char* ssid     = "NTFiber_30D8_2.4G";
const char* password = "C47D6aqZ";
const char* unityIP  = "192.168.1.23";   // laptop's IP running Unity (ipconfig)
const int   unityPort = 4210;
// ---------------------------------

const char* HAND = "RIGHT";    // do not change on this board
float gyroThreshold = 60.0;    // deg/s — tune this after testing
const int sendInterval = 50;   // ms -> ~20 packets/sec

const int MPU_ADDR = 0x68;
// Gyro sensitivity for default full-scale range (+/-250 deg/s) = 131 LSB per deg/s
const float GYRO_SENSITIVITY = 131.0;

const int LED_PIN = 2;    // WiFi status LED pin
const int SWITCH_PIN = 27; // Data-gate limit switch pin (INPUT_PULLUP)
const unsigned long debounceDelay = 50; // ms

WiFiUDP udp;
unsigned long lastSend = 0;
unsigned long lastBlinkToggle = 0;
bool ledBlinkState = false;

// Debounce state for the limit switch
bool switchPressed = false;        // debounced, "true" reading used by the rest of the code
bool lastRawSwitchState = HIGH;    // last raw pin reading (HIGH = not pressed, due to pull-up)
unsigned long lastDebounceTime = 0;

int16_t accX, accY, accZ;
int16_t gyroX, gyroY, gyroZ;

bool wakeUpMPU() {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x6B);   // PWR_MGMT_1 register
  Wire.write(0);      // wake up (clear sleep bit)
  return Wire.endTransmission(true) == 0;
}

void readMPU() {
  Wire.beginTransmission(MPU_ADDR);
  Wire.write(0x3B);   // starting register for accel readings
  Wire.endTransmission(false);
  Wire.requestFrom(MPU_ADDR, 14, true);

  accX  = Wire.read() << 8 | Wire.read();
  accY  = Wire.read() << 8 | Wire.read();
  accZ  = Wire.read() << 8 | Wire.read();
  Wire.read(); Wire.read();  // skip temperature
  gyroX = Wire.read() << 8 | Wire.read();
  gyroY = Wire.read() << 8 | Wire.read();
  gyroZ = Wire.read() << 8 | Wire.read();
}

// Call this every loop() iteration — non-blocking debounce for the limit switch.
// Updates the global `switchPressed` with a clean, debounced reading.
void updateSwitchState() {
  bool rawReading = digitalRead(SWITCH_PIN);   // LOW = pressed (pull-up wiring)

  if (rawReading != lastRawSwitchState) {
    lastDebounceTime = millis();   // reading changed -> restart debounce timer
  }

  if (millis() - lastDebounceTime > debounceDelay) {
    // reading has been stable longer than debounceDelay -> accept it
    switchPressed = (rawReading == LOW);
  }

  lastRawSwitchState = rawReading;
}

// Call this every loop() iteration — handles LED state without blocking
void updateWiFiLED() {
  if (WiFi.status() == WL_CONNECTED) {
    digitalWrite(LED_PIN, HIGH);   // connected -> solid ON
  } else {
    // disconnected -> non-blocking blink every 200ms
    if (millis() - lastBlinkToggle > 200) {
      ledBlinkState = !ledBlinkState;
      digitalWrite(LED_PIN, ledBlinkState ? HIGH : LOW);
      lastBlinkToggle = millis();
    }
  }
}

void setup() {
  Serial.begin(115200);

  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);   // start with LED off

  pinMode(SWITCH_PIN, INPUT_PULLUP);   // limit switch: HIGH = not pressed, LOW = pressed

  Wire.begin(21, 22);
  Wire.setClock(100000);
  delay(100);

  bool mpuFound = false;
  for (int attempt = 0; attempt < 5; attempt++) {
    if (wakeUpMPU()) {
      mpuFound = true;
      break;
    }
    Serial.println("MPU6050 not responding, retrying...");
    delay(300);
  }

  if (!mpuFound) {
    Serial.println("MPU6050 not found - check wiring!");
    while (1) delay(10);
  }
  Serial.println("MPU6050 found and woken up!");

  WiFi.begin(ssid, password);
  Serial.print("Connecting to WiFi");
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
    updateWiFiLED();   // keep LED blinking while connecting
  }
  Serial.println();
  Serial.print("WiFi connected. IP: ");
  Serial.println(WiFi.localIP());

  digitalWrite(LED_PIN, HIGH);   // connected -> LED solid ON

  udp.begin(unityPort);

  Serial.print("Ready. Sending UDP packets to ");
  Serial.print(unityIP);
  Serial.print(":");
  Serial.println(unityPort);
}

void loop() {
  // Update WiFi status LED every loop (non-blocking)
  updateWiFiLED();

  // Update the debounced limit switch reading every loop (non-blocking)
  updateSwitchState();

  // Auto-reconnect WiFi if it drops (important for battery-powered field use)
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi disconnected, reconnecting...");
    WiFi.disconnect();
    WiFi.begin(ssid, password);

    unsigned long reconnectStart = millis();
    while (WiFi.status() != WL_CONNECTED && millis() - reconnectStart < 10000) {
      delay(500);
      Serial.print(".");
      updateWiFiLED();   // keep blinking while reconnecting
    }

    if (WiFi.status() == WL_CONNECTED) {
      Serial.println();
      Serial.print("Reconnected! IP: ");
      Serial.println(WiFi.localIP());
      digitalWrite(LED_PIN, HIGH);   // reconnected -> solid ON
    } else {
      Serial.println();
      Serial.println("Reconnect failed, will retry next loop.");
      return;   // skip sensor read/send this cycle, try reconnecting again next loop
    }
  }

  readMPU();

  float gx = gyroX / GYRO_SENSITIVITY;
  float gy = gyroY / GYRO_SENSITIVITY;
  float gz = gyroZ / GYRO_SENSITIVITY;
  float gyroMag = sqrt(gx * gx + gy * gy + gz * gz);

  String state = (gyroMag > gyroThreshold) ? "MOVING" : "STILL";

  // Data gate: MPU is always read above, but we only forward real motion data
  // to Unity while the limit switch is held. Otherwise we send HOLD so Unity
  // freezes the plane instead of drifting from idle sensor jitter.
  if (!switchPressed) {
    state = "HOLD";
  }

  if (millis() - lastSend > sendInterval) {
    String packet = String(HAND) + "," + state + "," + String(gyroMag, 1);

    udp.beginPacket(unityIP, unityPort);
    udp.print(packet);
    int result = udp.endPacket();

    Serial.print("Sent -> ");
    Serial.print(packet);
    Serial.print("  to ");
    Serial.print(unityIP);
    Serial.print(":");
    Serial.print(unityPort);
    Serial.println(result == 1 ? "  [OK]" : "  [FAILED]");

    lastSend = millis();
  }
}
