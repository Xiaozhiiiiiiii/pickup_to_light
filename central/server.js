import express from "express";
import cors from "cors";
import fs from "fs";
import path from "path";
import mqtt from "mqtt";

// -------------------- ENV --------------------
const HTTP_PORT   = process.env.HTTP_PORT   || 8080;
const MQTT_URL    = process.env.MQTT_URL    || "mqtt://mqtt:1883"; // 你已在 compose 設定 mqtt://10.0.60.96:1883
const SEH_PRODUCT = process.env.SEH_PRODUCT || "SEH100";
const SEH_DEVICE_ID = process.env.SEH_DEVICE_ID || "EU-MuddledBrokenDiet";

// -------------------- CONFIG FILES --------------------
// 1) 靜態庫位對應（仍可用）：central/mapping.json
const mapping = JSON.parse(fs.readFileSync("./mapping.json", "utf-8"));
const BIN_MAP = mapping.bins || {};
const DEFAULT_COLOR = mapping.defaultColor || "COLCYAN";

// 2) 訂單對應外部檔：central/config/orders.json
const ORDER_CONFIG_PATH = path.resolve("./config/orders.json");
let orderConfig = {};
try {
  orderConfig = JSON.parse(fs.readFileSync(ORDER_CONFIG_PATH, "utf-8"));
  console.log("[CONFIG] Loaded order mapping from", ORDER_CONFIG_PATH);
} catch (e) {
  console.warn("[CONFIG] No orders.json found, using request/mapping only");
}
// 熱更新：檔案有變動自動載入（失敗不會中斷服務）
try {
  fs.watchFile(ORDER_CONFIG_PATH, { interval: 1000 }, () => {
    try {
      const next = JSON.parse(fs.readFileSync(ORDER_CONFIG_PATH, "utf-8"));
      orderConfig = next;
      console.log("[CONFIG] Reloaded orders.json");
    } catch (err) {
      console.error("[CONFIG] Reload error:", err.message);
    }
  });
} catch (_) { /* Windows 無權限時略過 */ }

// -------------------- STATE --------------------
const orders = new Map(); // orderId -> { orderId, lines, status, mode }

// -------------------- MQTT --------------------
console.log("[MQTT] Connecting to", MQTT_URL);
const mqttClient = mqtt.connect(MQTT_URL, {
  clean: true,
  reconnectPeriod: 2000, // 2s 自動重連
});

mqttClient.on("connect", () => {
  console.log("[MQTT] Connected to", MQTT_URL);
  mqttClient.subscribe("caneo/+/event", (err) => {
    if (err) console.error("[MQTT] subscribe error:", err);
    else console.log("[MQTT] Subscribed caneo/+/event");
  });
});
mqttClient.on("reconnect", () => console.log("[MQTT] Reconnecting..."));
mqttClient.on("offline",   () => console.log("[MQTT] Offline"));
mqttClient.on("close",     () => console.log("[MQTT] Close"));
mqttClient.on("error",     (e) => console.error("[MQTT] Error:", e.message));

mqttClient.on("message", (topic, payload) => {
  try {
    const msg = JSON.parse(payload.toString());
    handleButtonEvent(topic, msg);
  } catch (e) {
    console.warn("[MQTT] Non-JSON message:", topic, payload.toString());
  }
});

// -------------------- HTTP --------------------
const app = express();
app.use(express.json());
app.use(cors());

// Health
app.get("/health", (req, res) => res.json({ ok: true }));

// （可查看目前載入的 orders.json）
app.get("/config/orders", (req, res) => {
  res.json({ from: ORDER_CONFIG_PATH, data: orderConfig });
});

// Create order
app.post("/orders", async (req, res) => {
  const { orderId, lines, mode } = req.body || {};
  if (!orderId) return res.status(400).json({ error: "orderId required" });

  // 建立記錄（lines 可能會被外部 orders.json 覆蓋，所以先記原始）
  orders.set(orderId, { orderId, lines: Array.isArray(lines) ? lines : [], status: "IN_PROGRESS", mode: mode || "GUIDE" });
  console.log(`[ORDER] ${orderId} created`);

  // 先看 orders.json 是否有預先定義
  const predef = orderConfig[orderId];

  if (Array.isArray(predef) && predef.length > 0) {
    console.log(`[ORDER] Using predefined LED layout for ${orderId} (orders.json)`);
    for (const it of predef) {
      // 每個 item: { strip, start, stop, color }
      const color = it.color || DEFAULT_COLOR;
      await ledRangeStrip(it.strip, it.start, it.stop, color, 1, 190);
    }
  } else {
    // 沒有預先定義 → 使用 request body 的 lines + mapping.json
    if (!Array.isArray(lines) || lines.length === 0) {
      return res.status(400).json({ error: "lines[] required when orders.json has no mapping for this orderId" });
    }
    console.log(`[ORDER] Using request body layout for ${orderId}`);
    for (const line of lines) {
      const bin = line.binId;
      const color = line.color || DEFAULT_COLOR;
      const map = BIN_MAP[bin];
      if (!map) {
        console.warn(`[ORDER] ${orderId} bin ${bin} not found in mapping.json`);
        continue;
      }
      await ledRangeStrip(map.strip, map.start, map.stop, color, 1, 190);
    }
  }

  return res.json({ ok: true, orderId, status: "IN_PROGRESS" });
});

// Manual confirm (optional)
app.post("/confirm", async (req, res) => {
  const { orderId, binId } = req.body || {};
  if (!orderId || !binId) return res.status(400).json({ error: "orderId and binId required" });
  await confirmBin(orderId, binId);
  return res.json({ ok: true });
});

// List orders
app.get("/orders", (req, res) => {
  res.json(Array.from(orders.values()));
});

// （可選）清空全部 LED 的端點，方便測試
app.post("/clearAll", async (_req, res) => {
  await ledClearAllStrip();
  res.json({ ok: true });
});

// -------------------- LED (LedStrip) --------------------
const COLOR_MAP = {
  COLRED:    { R:150, G:0,   B:0 },
  COLGREEN:  { R:0,   G:150, B:0 },
  COLBLUE:   { R:0,   G:0,   B:150 },
  COLYELLOW: { R:150, G:150, B:0 },
  COLCYAN:   { R:0,   G:150, B:150 },
  COLMAGENTA:{ R:150, G:0,   B:150 },
  COLWHITE:  { R:180, G:180, B:180 },
  COLBLACK:  { R:0,   G:0,   B:0 }
};
function toRGB(color) {
  if (typeof color === "string" && COLOR_MAP[color]) return COLOR_MAP[color];
  if (typeof color === "object" && "R" in color) return color;
  return COLOR_MAP.COLCYAN;
}

function topicLedStrip() {
  return `captron.com/${SEH_PRODUCT}/nd/${SEH_DEVICE_ID}/Set/Data/LedStrip`;
}
function publish(topic, obj) {
  const msg = JSON.stringify(obj);
  mqttClient.publish(topic, msg);
  console.log("[MQTT➜]", topic, msg);
}

// 亮燈
async function ledRangeStrip(stripName, startIdx, stopIdx, color="COLCYAN", effect=1, speed=190) {
  const rgb = toRGB(color);
  const payload = {
    Content: "{Content definition}",
    [stripName]: {
      Active: true,
      Segments: [{
        StartLED: startIdx,
        StopLED:  stopIdx,
        Speed:    speed,
        Effect:   effect,
        Colors:   [rgb, rgb]
      }]
    }
  };
  publish(topicLedStrip(), payload);
}

// 確認亮綠→清除
async function ledConfirmStrip(stripName, startIdx, stopIdx) {
  const green = {R:0,G:150,B:0};
  const black = {R:0,G:0,B:0};
  publish(topicLedStrip(), {
    Content: "{Content definition}",
    [stripName]: {
      Active: true,
      Segments: [{ StartLED:startIdx, StopLED:stopIdx, Speed:0, Effect:0, Colors:[green,green] }]
    }
  });
  await new Promise(r=>setTimeout(r,800));
  publish(topicLedStrip(), {
    Content: "{Content definition}",
    [stripName]: {
      Active: true,
      Segments: [{ StartLED:startIdx, StopLED:stopIdx, Speed:0, Effect:0, Colors:[black,black] }]
    }
  });
}

// 清除所有段（此處示範清單一條帶；如有多條帶可擴充）
async function ledClearAllStrip() {
  const black = {R:0,G:0,B:0};
  publish(topicLedStrip(), {
    Content: "{Content definition}",
    LED_STRIP_1: {
      Active: true,
      Segments: [{ StartLED:0, StopLED:70, Speed:0, Effect:0, Colors:[black,black] }]
    }
  });
}

// -------------------- BUTTON HANDLER --------------------
async function handleButtonEvent(topic, msg) {
  if (!msg || !msg.pressed) return;
  const targetBins = Object.entries(BIN_MAP)
    .filter(([_, m]) => m.buttonTopic === topic)
    .map(([binId]) => binId);
  if (targetBins.length === 0) return;

  console.log("[BTN]", topic, "pressed -> bins:", targetBins);
  for (const od of orders.values()) {
    if (od.status !== "IN_PROGRESS") continue;
    for (const b of targetBins) await confirmBin(od.orderId, b);
  }
}

async function confirmBin(orderId, binId) {
  const od = orders.get(orderId);
  if (!od) return;
  const map = BIN_MAP[binId];
  if (map) await ledConfirmStrip(map.strip, map.start, map.stop);
  for (const line of od.lines) {
    if (line.binId === binId) line.done = true;
  }
  if (od.lines.every(l => l.done)) {
    od.status = "DONE";
    console.log(`[ORDER] ${orderId} DONE`);
    await ledClearAllStrip();
  }
}

// -------------------- BOOT --------------------
app.listen(HTTP_PORT, () => {
  console.log(`[HTTP] listening on :${HTTP_PORT}`);
  console.log(`[INFO] Using ${SEH_PRODUCT} DeviceId=${SEH_DEVICE_ID}`);
});
