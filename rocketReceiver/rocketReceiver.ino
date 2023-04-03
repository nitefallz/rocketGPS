#include <BLEDevice.h>
#include <BLEUtils.h>
#include <BLEServer.h>
#include <BLE2902.h>
#include "LoRaWan_APP.h"
#include "HT_SSD1306Wire.h"
SSD1306Wire oled(0x3c, 500000, SDA_OLED, SCL_OLED, GEOMETRY_128_64, RST_OLED);

#define RF_FREQUENCY 915000000
#define LORA_BANDWIDTH 0
#define LORA_SPREADING_FACTOR 12
#define LORA_CODINGRATE 4
#define LORA_PREAMBLE_LENGTH 12
#define LORA_SYMBOL_TIMEOUT 0
#define LORA_FIX_LENGTH_PAYLOAD_ON false
#define LORA_IQ_INVERSION_ON false
#define BUTTON_PIN 0

#define BUFFER_SIZE 30
#define SERVICE_UUID "4fafc201-1fb5-459e-8fcc-c5c9c331914b"
#define CHARACTERISTIC_UUID "beb5483e-36e1-4688-b7f5-ea07361b26a8"

char rxpacket[BUFFER_SIZE];

static RadioEvents_t RadioEvents;

bool newPacketReceived = false;

int16_t Rssi, rxSize;

bool lora_idle = true;
bool displayOn = true;
bool buttonState = false;
bool deviceConnected = false;
bool oldDeviceConnected = false;

char gpsCoord[200];
char lastGpsCoord[200];

BLEServer* pServer = NULL;
BLECharacteristic* pCharacteristic = NULL;

class MyServerCallbacks : public BLEServerCallbacks {
  void onWrite(BLEServer* pCharacteristic) {
    Serial.println("Received a write request:");
  }

  void onConnect(BLEServer* pServer) {
    deviceConnected = true;
    BLEDevice::startAdvertising();
  };

  void onDisconnect(BLEServer* pServer) {
    deviceConnected = false;
  };
};

void setup() {
  Serial.begin(115200);
  pinMode(BUTTON_PIN, INPUT_PULLUP);
  VextON();
  setupBluetooth();

  delay(100);
  oled.setFont(ArialMT_Plain_10);
  oled.init();
  oled.drawString(0, 0, "Init...");
  oled.display();
  delay(2000);

  Mcu.begin();
  gpsCoord[0] = '\0';
  Rssi = 0;

  RadioEvents.RxDone = OnRxDone;
  Radio.Init(&RadioEvents);
  Radio.SetChannel(RF_FREQUENCY);
  Radio.SetRxConfig(MODEM_LORA, LORA_BANDWIDTH, LORA_SPREADING_FACTOR,
                    LORA_CODINGRATE, 0, LORA_PREAMBLE_LENGTH,
                    LORA_SYMBOL_TIMEOUT, LORA_FIX_LENGTH_PAYLOAD_ON,
                    0, true, 0, 0, LORA_IQ_INVERSION_ON, true);
}

unsigned long previousMillis = 0;
const long interval = 2000;

void setupBluetooth() {
  BLEDevice::init("ESP32_GPS");
  pServer = BLEDevice::createServer();
  pServer->setCallbacks(new MyServerCallbacks());

  // Create the BLE Service
  BLEService* pService = pServer->createService(SERVICE_UUID);

  // Create a BLE Characteristic
  pCharacteristic = pService->createCharacteristic(
    CHARACTERISTIC_UUID,
    BLECharacteristic::PROPERTY_READ | BLECharacteristic::PROPERTY_WRITE | BLECharacteristic::PROPERTY_NOTIFY | BLECharacteristic::PROPERTY_INDICATE);

  // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.descriptor.gatt.client_characteristic_configuration.xml
  // Create a BLE Descriptor
  pCharacteristic->addDescriptor(new BLE2902());

  // Start the service
  pService->start();

  // Start advertising
  BLEAdvertising* pAdvertising = BLEDevice::getAdvertising();
  pAdvertising->addServiceUUID(SERVICE_UUID);
  pAdvertising->setScanResponse(false);
  pAdvertising->setMinPreferred(0x0);  // set value to 0x00 to not advertise this parameter
  BLEDevice::startAdvertising();
  Serial.println("Bluetooth device is ready to pair");
}


void VextON(void) {
  pinMode(Vext, OUTPUT);
  digitalWrite(Vext, LOW);
}

void VextOFF(void) {
  pinMode(Vext, OUTPUT);
  digitalWrite(Vext, HIGH);
}

bool isButtonPressed() {
  return digitalRead(BUTTON_PIN) == LOW;
}

uint8_t calculateXORChecksum(const char* data) {
  uint8_t checksum = 0;
  for (size_t i = 0; data[i] != '\0'; ++i) {
    checksum ^= data[i];
  }
  return checksum;
}

bool verifyChecksum(const char* receivedData, uint8_t receivedChecksum) {
  uint8_t calculatedChecksum = calculateXORChecksum(receivedData);
  return receivedChecksum == calculatedChecksum;
}

void processReceivedPacket(const char* packet) {
  char receivedData[256];
  unsigned int receivedChecksum;
  if (sscanf(packet, "%[^*]*%02X", receivedData, &receivedChecksum) == 2) {
    uint8_t trimmedChecksum = static_cast<uint8_t>(receivedChecksum);
    if (verifyChecksum(receivedData, trimmedChecksum)) {
      // Checksum is valid, process the received GPS coordinates
      snprintf(gpsCoord, sizeof(receivedData), "%s", receivedData);
      Serial.println("New packet rx");
      newPacketReceived = true;
    } else {
      // Checksum is invalid, handle the error
      Serial.println("Checksum error");
      newPacketReceived = false;
    }
  } else {
    Serial.println("Format error");

    Serial.printf("%s", receivedData);
    newPacketReceived = false;
  }
}

void displayCoord() {
  char buf[200];
  oled.clear();
  if (newPacketReceived) {
    newPacketReceived = false;
    if (strcmp(gpsCoord, "0.000000,0.000000") == 0) {
      sprintf(buf, "Waiting for GPS\n \nStrength: %d\nLoRA RX+", Rssi);
      oled.drawString(0, 0, buf);
    } else {
      char latitude[20];
      char longitude[20];
      sscanf(gpsCoord, "%[^,],%s", latitude, longitude);
      snprintf(lastGpsCoord, sizeof(gpsCoord), "%s", gpsCoord);
      sprintf(buf, "%s\n%s\nStrength: %d\nLoRA RX+", latitude, longitude, Rssi);
      oled.drawString(0, 0, buf);
      if (deviceConnected) {        
        pCharacteristic->setValue(gpsCoord);
        pCharacteristic->notify();
        Serial.println("Sending BLE data");
        delay(250);  // bluetooth stack will go into congestion, if too many packets are sent, in 6 hours test i was able to go as low as 3ms
      }
    }
  } else {
    char latitude[20];
    char longitude[20];
    sscanf(lastGpsCoord, "%[^,],%s", latitude, longitude);
    sprintf(buf, "%s\n%s\nStrength: %d\nLoRA RX--", latitude, longitude, Rssi);
    oled.drawString(0, 0, buf);
  }

  oled.display();
  gpsCoord[0] = '\0';
}

void toggleDisplay() {
  if (displayOn) {
    displayOn = false;
    oled.clear();
    oled.displayOff();
  } else {
    displayOn = true;
    oled.displayOn();
    displayCoord();
  }
}

void OnRxDone(uint8_t* payload, uint16_t size, int16_t rssi, int8_t snr) {
  Rssi = rssi;
  rxSize = size;

  if (rxSize == 0) {
    Serial.printf("no rx");
    newPacketReceived = false;
    return;
  }
  memcpy(rxpacket, payload, size);
  rxpacket[size] = '\0';
  Radio.Sleep();
  Serial.printf("\r\nreceived packet %s with rssi %d , length %d\r\n", rxpacket, rssi, rxSize);
  processReceivedPacket(rxpacket);
  lora_idle = true;
}

void loop() {
  if (isButtonPressed()) {
    if (!buttonState) {
      toggleDisplay();
      buttonState = true;
    }
  } else {
    buttonState = false;
  }

  if (lora_idle) {
    lora_idle = false;
    Radio.Rx(0);
  }

  Radio.IrqProcess();

  unsigned long currentMillis = millis();
  if (currentMillis - previousMillis >= interval) {
    previousMillis = currentMillis;
    //Serial.println("Displaying...");
    displayCoord();
  }

  // disconnecting
  if (!deviceConnected && oldDeviceConnected) {
    delay(500);                   // give the bluetooth stack the chance to get things ready
    pServer->startAdvertising();  // restart advertising
    oldDeviceConnected = deviceConnected;
  }
  // connecting
  if (deviceConnected && !oldDeviceConnected) {
    oldDeviceConnected = deviceConnected;
  }
}