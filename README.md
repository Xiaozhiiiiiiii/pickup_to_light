# Pick-to-Light Quickstart (Docker)

## 1) Edit these before start
- In `docker-compose.yml`, set `SEH_DEVICE_ID=YOUR_DEVICE_ID` (from your SEH100 label).
- In `central/mapping.json`, map your bin IDs to LED strip ranges and button topics.

## 2) Start the stack
```bash
docker compose up -d
```

## 3) Send sample orders
```bash
docker compose exec erp-sim python sim.py --bins A-01-01,A-01-02 --start 1 --end 1 --color COLYELLOW
```

## 4) Simulate button press (if you don't have CANEO wired yet)
Use MQTTX or mosquitto_pub to publish a press to a matching topic from mapping.json:
```bash
mosquitto_pub -h 127.0.0.1 -p 1883 -t "caneo/1/event" -m '{ "port": 1, "pressed": true, "ts": "2025-10-16T10:00:00+08:00" }'
```

When the event is received, the central will turn that bin's range green briefly and clear it. When all lines are done the order is marked DONE and a CLEAR is issued.