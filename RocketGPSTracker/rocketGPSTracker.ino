// RocketGPSLora
#include "GPS_Air530Z.h"
#include "LoRaWan_APP.h"

Air530ZClass GPS;

// Constants

#define RF_FREQUENCY 915000000   // Hz
#define TX_OUTPUT_POWER 20       // dBm
#define LORA_BANDWIDTH 0         // [0: 125 kHz, \
                                 //  1: 250 kHz, \
                                 //  2: 500 kHz, \
                                 //  3: Reserved]
#define LORA_SPREADING_FACTOR 12  // [SF7..SF12]
#define LORA_CODINGRATE 4        // [1: 4/5, \
                                 //  2: 4/6, \
                                 //  3: 4/7, \
                                 //  4: 4/8]
#define LORA_PREAMBLE_LENGTH 12   // Same for Tx and Rx
#define LORA_FIX_LENGTH_PAYLOAD_ON false
#define LORA_IQ_INVERSION_ON false
#define BUFFER_SIZE 255  // Define the payload size here


#define timetillsleep 60000
#define timetillwakeup 10000

const uint32_t GPS_READ_INTERVAL_MS = 1000;  // interval between GPS reads

const uint32_t DISPLAY_UPDATE_INTERVAL_MS = 500;
const uint32_t BUTTON_DEBOUNCE_INTERVAL_MS = 50;

static TimerEvent_t sleep;
static TimerEvent_t wakeUp;
uint32_t lastGPSSendTime = 0;



#define BUTTON_PIN P3_3

// Global variables
bool displayOn = true;
bool buttonPressedHandled = false;


double last_lat = 0;
double last_lng = 0;

char txpacket[BUFFER_SIZE];

static RadioEvents_t RadioEvents;
bool lora_idle = true;
int16_t txNumber;

// Forward declarations
void displayInfoOnScreen();
void displayInfoOverSerial();
void VextON();

// Setup
void setup() {

	Serial.begin(115200);
	pinMode(BUTTON_PIN, INPUT_PULLUP);

	RadioEvents.TxDone = OnTxDone;
	RadioEvents.TxTimeout = OnTxTimeout;
	Radio.Init(&RadioEvents);
	Radio.SetChannel(RF_FREQUENCY);
	Radio.SetTxConfig(MODEM_LORA, TX_OUTPUT_POWER, 0, LORA_BANDWIDTH,
		LORA_SPREADING_FACTOR, LORA_CODINGRATE,
		LORA_PREAMBLE_LENGTH, LORA_FIX_LENGTH_PAYLOAD_ON,
		true, 0, 0, LORA_IQ_INVERSION_ON, 3000);
	VextON();
	display.init();
	display.clear();
	display.display();
	display.setFont(ArialMT_Plain_10);

	delay(1000);
	GPS.setmode(MODE_GPS_GLONASS);
	GPS.setNMEA(NMEA_GGA);
	GPS.begin();
	displayInfoOnScreen();
	displayInfoOverSerial();
}

// Utility functions
void readAndEncodeGPSData() {
	while (GPS.available() > 0) {
		GPS.encode(GPS.read());
	}
}


void VextON() {
	pinMode(Vext, OUTPUT);
	digitalWrite(Vext, LOW);
}


void VextOFF() {
	pinMode(Vext, OUTPUT);
	digitalWrite(Vext, HIGH);
}


void OnTxDone() {
	turnOffRGB();
	Serial.println("TX done......");
	lora_idle = true;
}

void OnTxTimeout() {
	turnOffRGB();
	Radio.Sleep();
	Serial.println("TX Timeout......");
	lora_idle = true;
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

float readBatteryVoltage() {
	float batteryVoltage = 0;
	batteryVoltage = getBatteryVoltage();
	return batteryVoltage;
}

void toggleDisplay() {
	if (displayOn) {
		displayOn = false;
		display.clear();
		display.displayOff();
	}
	else {
		displayOn = true;
		display.displayOn();
	}
}

// Display functions
void displayInfoOnScreen() {
	const char DATE_TIME_STR[] PROGMEM = "D/T: ";
	const char INVALID_STR[] PROGMEM = "INVALID";
	const char LAT_STR[] PROGMEM = "LAT: ";
	const char LON_STR[] PROGMEM = "LON: ";
	const char ALT_STR[] PROGMEM = "ALT: ";
	const char SATS_STR[] PROGMEM = "SATS: ";
	const char HDOP_STR[] PROGMEM = "ACC: ";
	const char SPEED_STR[] PROGMEM = "SPEED: ";

	char buf[300];

	display.clear();
	float batteryVoltage = readBatteryVoltage();
	if (GPS.date.isValid())
		sprintf_P(buf, PSTR("%s%d/%02d/%02d %02d:%02d:%02d.%02d\n"), DATE_TIME_STR, GPS.date.year(), GPS.date.month(), GPS.date.day(), GPS.time.hour(), GPS.time.minute(), GPS.time.second(), GPS.time.centisecond());
	else
		sprintf_P(buf, PSTR("%s%s\n"), DATE_TIME_STR, INVALID_STR);

	// Print the latitude to the buffer
	sprintf_P(&buf[strlen(buf)], PSTR("%s%-2.3f - "), LAT_STR, GPS.location.lat());

	// Print the longitude to the buffer
	sprintf_P(&buf[strlen(buf)], PSTR("%s%-2.3f\n"), LON_STR, GPS.location.lng());

	// Print the altitude to the buffer
	sprintf_P(&buf[strlen(buf)], PSTR("%s%-2.3f - "), ALT_STR, GPS.altitude.meters());

	// Print the satellite information to the buffer
	sprintf_P(&buf[strlen(buf)], PSTR("%s%d\n%s%-2.0f, Batt: %.2fV\n%s%-2.1f\n"), SATS_STR, GPS.satellites.value(), HDOP_STR, GPS.hdop.hdop(), batteryVoltage, SPEED_STR, GPS.speed.mph());

	// Write the buffer to the screen
	display.drawString(0, 0, buf);

	// Update the display
	display.display();
}

void displayInfoOverSerial() {
	Serial.print(F("Date/Time: "));
	if (GPS.date.isValid()) {
		Serial.printf("%d/%02d/%02d", GPS.date.year(), GPS.date.day(), GPS.date.month());
	}
	else {
		Serial.print(F("INVALID"));
	}

	if (GPS.time.isValid()) {
		Serial.printf(" %02d:%02d:%02d.%02d", GPS.time.hour(), GPS.time.minute(), GPS.time.second(), GPS.time.centisecond());
	}
	else {
		Serial.print(F(" INVALID"));
	}
	Serial.println();

	Serial.print(F("LAT: "));
	Serial.print(GPS.location.lat(), 6);
	Serial.print(F(", LON: "));
	Serial.print(GPS.location.lng(), 6);
	Serial.print(F(", ALT: "));
	Serial.print(GPS.altitude.meters());

	Serial.println();

	Serial.print(F("SATS: "));
	Serial.print(GPS.satellites.value());
	Serial.print(F(", HDOP: "));
	Serial.print(GPS.hdop.hdop());
	Serial.print(F(", AGE: "));
	Serial.print(GPS.location.age());
	Serial.print(F(", COURSE: "));
	Serial.print(GPS.course.deg());
	Serial.print(F(", SPEED: "));
	Serial.println(GPS.speed.mph());
	Serial.println();
}


void sendGPSInfoLora() {
	double current_lat = GPS.location.lat();
	double current_lng = GPS.location.lng();

	if (lora_idle && (last_lat != current_lat || last_lng != current_lng || millis() - lastGPSSendTime >= 300000)) { // Only send if GPS data has changed or if 5 minutes have passed since the last transmission
		last_lat = current_lat;
		last_lng = current_lng;
		lastGPSSendTime = millis(); // Update the last send time

		char packetToSend[256];  // Make sure the buffer is large enough
		txNumber += 0.01;
		sprintf(txpacket, "%f,%f", current_lat, current_lng);  //start a package
		uint8_t checksum = calculateXORChecksum(txpacket);

		snprintf(packetToSend, sizeof(packetToSend), "%s*%02X", txpacket, checksum);

		Serial.printf("\r\nsending packet \"%s\" , length %d\r\n", packetToSend, strlen(packetToSend));
		display.drawString(80, 50, "Lora XMIT");
		display.display();
		turnOnRGB(COLOR_SEND, 0);                                  //change rgb color
		Radio.Send((uint8_t*)packetToSend, strlen(packetToSend));  //send the package out
		lora_idle = false;
	}
}

// Loop
void loop() {
	static uint32_t lastGPSTime = 0;
	static uint32_t lastDisplayUpdateTime = 0;

	if (isButtonPressed()) {
		if (!buttonPressedHandled) {
			toggleDisplay();
			buttonPressedHandled = true;
		}
	}
	else
		buttonPressedHandled = false;

	uint32_t currentTime = millis();
	if (currentTime - lastGPSTime >= GPS_READ_INTERVAL_MS) {
		lastGPSTime = currentTime;

		while (GPS.available() > 0) {
			GPS.encode(GPS.read());
		}

		// Update display and transmit data immediately after GPS data is available
		displayInfoOnScreen();
		displayInfoOverSerial();
		sendGPSInfoLora();
	}
}
