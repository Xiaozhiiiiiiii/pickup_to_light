// SmartCap.Bridge / Form1.cs —— 連上即全滅；GetOrder 只列；Start 才開始；三顆按鈕 → 三段LED（以 line 推進）；整單完成全滅 + 等待下一張
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using Newtonsoft.Json.Linq;
//TESTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
using smartCAP_SDK;
using smartCAP_SDK.Messages;
using smartCAP_SDK.Common.Models; // SensorhubFinder

namespace SmartCap.Bridge
{
    public partial class Form1 : Form
    {
        // ===== 多張工單佇列（每次 Get Order 取下一張） =====
        private JArray? _orderQueue;
        private JObject? _currentOrder;

        // ===== SmartCAP =====
        private SmartCAPService? _svc;
        private readonly string defaultAddress = "ALL_SENSORS";

        // ===== 內部狀態：以「line」為單位推進 =====
        private JArray? _lines;                  // _currentOrder["lines"]
        private int _lineIndex = -1;             // 目前指到第幾個 line
        private bool _inLedPhase = false;        // 是否處於 LED 區段（遇到第一個 LED line 就會進入）
        private bool _hubConnecting = false;

        // 常數
        private const string LedSwitchButton = "1008";      // LED 切換用按鈕
        private const string LedHubIpDefault = "10.0.60.96";

        // 防抖（避免 1008 連續回報）
        private DateTime _last1008At = DateTime.MinValue;
        private bool _handling1008 = false;

        public Form1()
        {
            InitializeComponent();

            btnConnectHub.Click += btnConnectHub_Click;
            btnGetOrder.Click += btnGetOrder_Click;
            btnStart.Click += btnStart_Click;

            UpdateHubStatus("HUB: Not connected");
        }

        // ================== 連 HUB ==================
        private async void btnConnectHub_Click(object? sender, EventArgs e)
        {
            if (_hubConnecting) return;
            _hubConnecting = true;
            btnConnectHub.Enabled = false;
            UpdateHubStatus("⏳ Connecting...");

            try
            {
                string manualIp = txtHubIp.Text?.Trim();
                bool ok = await TryConnectHubSequenceAsync(manualIp);
                if (ok)
                {
                    UpdateHubStatus("✅ HUB Connected");
                    SubscribeHubResponses();

                    // 連線後立即全滅（LED Hub + 按鈕）
                    await EnsureAllOffAsync();
                    Log("🔇 連線完成：所有 LED 與按鈕外觀已關閉，等待 Get Order。");
                }
                else
                {
                    UpdateHubStatus("❌ HUB Failed");
                }
            }
            catch (Exception ex)
            {
                UpdateHubStatus("❌ HUB Failed");
                Log("❌ HUB 例外: " + ex.Message);
            }
            finally
            {
                _hubConnecting = false;
                btnConnectHub.Enabled = true;
            }
        }

        private Task<bool> TryConnectHubSequenceAsync(string? manualIp)
        {
            return Task.Run(() =>
            {
                try
                {
                    _svc = new SmartCAPService();

                    // (1) 手動 IP 先試 4040 / 8883
                    if (!string.IsNullOrWhiteSpace(manualIp))
                    {
                        if (TryInitWithIp(_svc, manualIp, 4040, false, 4000) ||
                            TryInitWithIp(_svc, manualIp, 8883, true, 4000))
                            return true;

                        Log($"⚠️ 手動 IP 連線失敗：{manualIp}（改用 Finder 掃描）");
                    }

                    // (2) Finder 掃描
                    var hubs = SensorhubFinder.Find();
                    if (hubs != null && hubs.Count > 0)
                    {
                        foreach (var hub in hubs)
                        {
                            if (InvokeInitWithHub(_svc, hub))
                            {
                                Log("HUB 連線成功（Finder）： " + ExtractIpFromHub(hub));
                                return true;
                            }
                        }
                        Log("❌ Finder 掃到 HUB，但未成功連線。");
                    }
                    else
                    {
                        Log("❌ Finder 掃描不到任何 HUB。");
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Log("❌ TryConnectHubSequenceAsync 例外：" + ex.Message);
                    return false;
                }
            });
        }

        private bool TryInitWithIp(SmartCAPService svc, string ip, int port, bool useSsl, int timeoutMs)
        {
            try
            {
                var t = Task.Run(() => svc.Init(ip, port, useSsl));
                bool completed = t.Wait(timeoutMs);
                bool ok = completed && t.Result;
                Log($"Init({ip},{port},{useSsl}) -> {(ok ? "成功" : "失敗")}");
                return ok;
            }
            catch (Exception ex)
            {
                Log($"Init({ip},{port},{useSsl}) 例外：{ex.Message}");
                return false;
            }
        }

        private bool InvokeInitWithHub(SmartCAPService svc, object hub)
        {
            try
            {
                var tSvc = svc.GetType();
                var methods = tSvc.GetMethods().Where(m => m.Name == "Init").ToList();

                // 嘗試取出 IP
                var ip = ExtractIpFromHub(hub);
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    var init3 = methods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length == 3 &&
                               ps[0].ParameterType == typeof(string) &&
                               ps[1].ParameterType == typeof(int) &&
                               ps[2].ParameterType == typeof(bool);
                    });
                    if (init3 != null)
                    {
                        if ((init3.Invoke(svc, new object[] { ip, 4040, false }) as bool?) == true) return true;
                        if ((init3.Invoke(svc, new object[] { ip, 8883, true }) as bool?) == true) return true;
                    }

                    var initStr = methods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length == 1 && ps[0].ParameterType == typeof(string);
                    });
                    if (initStr != null)
                    {
                        var ret2 = initStr.Invoke(svc, new object[] { ip });
                        return ret2 is bool b2 ? b2 : true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log("InvokeInitWithHub 例外：" + ex.Message);
                return false;
            }
        }

        private string ExtractIpFromHub(object hub)
        {
            try
            {
                var t = hub.GetType();
                foreach (var name in new[] { "Ip", "IP", "IpAddress", "Address", "Host", "Hostname" })
                {
                    var p = t.GetProperty(name);
                    var v = p?.GetValue(hub)?.ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
                var s = hub.ToString();
                if (string.IsNullOrWhiteSpace(s)) return "";
                var m = Regex.Match(s, @"\b\d{1,3}(\.\d{1,3}){3}\b");
                return m.Success ? m.Value : "";
            }
            catch { return ""; }
        }

        // ================== Get Order：只列出，不啟動 ==================
        private void btnGetOrder_Click(object? sender, EventArgs e)
        {
            Log("🟦 [UI] Get Order");
            if (_svc == null) { Log("⚠️ 尚未連線 HUB"); return; }

            try
            {
                var json = BuildEmbeddedOrdersJson().Trim();
                if (!json.StartsWith("[")) json = "[" + json + "]";

                // 第一次或前一張完成後才載入佇列
                if (_orderQueue == null || _orderQueue.Count == 0)
                    _orderQueue = JArray.Parse(json);

                if (_orderQueue.Count == 0)
                {
                    Log("📭 沒有更多工單了。");
                    return;
                }

                _currentOrder = (JObject)_orderQueue[0];
                _orderQueue.RemoveAt(0);

                _lines = (JArray)_currentOrder["lines"]!;
                _lineIndex = -1;       // 等 Start 才開始走
                _inLedPhase = false;

                Log($"✅ 載入工單 {_currentOrder["orderId"]}，共 {_lines!.Count} 個料件（line）：");
                for (int i = 0; i < _lines.Count; i++)
                {
                    var ln = (JObject)_lines[i];
                    var part = ln.Value<string>("partNo");
                    var qty = ln.Value<int?>("qty") ?? 0;
                    var loc = ln.Value<string>("location");
                    Log($"   [{i + 1}] part={part} qty={qty} loc={loc}");
                }
                Log("👉 按下『Start』才會開始顯示第一顆。");
            }
            catch (Exception ex)
            {
                Log("❌ GetOrder 失敗：" + ex.Message);
            }
        }

        // ================== Start：從第 1 個 line 開始，建立 polling ==================
        private async void btnStart_Click(object? sender, EventArgs e)
        {
            Log("🟩 [UI] Start");
            if (_svc == null) { Log("⚠️ 尚未連線 HUB"); return; }
            if (_lines == null || _lines.Count == 0) { Log("⚠️ 尚未載入工單"); return; }

            _lineIndex = 0;
            _inLedPhase = false;

            // 建 polling 名單（所有 buttons + 1008）
            var poll = string.Join("/",
                _lines.OfType<JObject>()
                      .SelectMany(ln => ((JArray?)ln["buttons"])?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                      .Select(b => b.Value<string>("address") ?? "")
                      .Concat(new[] { LedSwitchButton })
                      .Where(a => !string.IsNullOrWhiteSpace(a))
                      .Distinct()
            );
            EnablePolling(poll);

            await ActivateFromCurrentLineAsync();
        }

        // ========= 依目前 _lineIndex 決定要做什麼（button line or led line）=========
        private async Task ActivateFromCurrentLineAsync()
        {
            if (_lines == null || _lineIndex < 0 || _lineIndex >= _lines.Count)
            {
                await FinishOrderAsync(); // 保守：若越界就視為完成
                return;
            }

            var line = (JObject)_lines[_lineIndex];
            var btns = (line["buttons"] as JArray) ?? new JArray();
            var steps = (line["ledSteps"] as JArray) ?? new JArray();

            // 有按鈕 → 點亮該顆
            if (btns.Count > 0)
            {
                _inLedPhase = false; // 保險
                var b = (JObject)btns[0]; // 每個 line 只有 1 顆
                string addr = b.Value<string>("address") ?? defaultAddress;
                string color = b.Value<string>("color") ?? "COLBLUE";
                string text = WrapAtIfNeeded(b.Value<string>("text") ?? "@@");

                ForceState(addr, "PRE");
                SendToHub(addr, "SET_PARAMETER", "PRE_BUTTON_MODE", $"ENABLED//{color}/FLASH_RING/{text}");

                var part = line.Value<string>("partNo") ?? "-";
                var qty = line.Value<int?>("qty") ?? 0;
                var loc = line.Value<string>("location") ?? "-";
                Log($"➡️ 引導 Line#{_lineIndex + 1}（Button） part={part} qty={qty} loc={loc} | 按鈕 {addr}（藍）");
                return;
            }

            // 沒有按鈕、有 LED → 進入 LED 階段
            if (steps.Count > 0)
            {
                _inLedPhase = true;
                await TriggerCurrentLedLineAsync(); // 使用目前 line 的單一 LED 段
                return;
            }

            // 這個 line 既沒有 buttons 也沒有 ledSteps → 略過它，進下一個
            _lineIndex++;
            await ActivateFromCurrentLineAsync();
        }

        // ========= 按鍵回報 =========
        private void SubscribeHubResponses()
        {
            try
            {
                if (_svc == null) return;
                _svc.Response -= SmartCAP_Response;
                _svc.Response += SmartCAP_Response;
                Log("🔔 已訂閱 SmartCAP Response。");
            }
            catch (Exception ex)
            {
                Log("❌ 訂閱回報事件失敗：" + ex.Message);
            }
        }

        private void SmartCAP_Response(object? sender, SensorhubResponseEventArgs e)
        {
            if (e?.Response == null) return;

            var msg = e.Response;
            string addr = msg.Address ?? "";
            string payload = msg.Payload ?? "";

            Log($"[HUB→SDK] {addr} | {msg.Offset ?? ""} | {payload}");

            bool isPress =
                payload.IndexOf("POST_PRESSED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                payload.IndexOf("SHORT_PRESS", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isPress) return;

            string effectiveAddr = GetEffectiveAddress(addr, payload);
            _ = HandlePressAsync(effectiveAddr);
        }

        private async Task HandlePressAsync(string pressedAddress)
        {
            if (_lines == null || _lineIndex < 0 || _lineIndex >= _lines.Count) return;

            if (!_inLedPhase)
            {
                // 按鈕階段：目前 line 應該是「button line」
                var line = (JObject)_lines[_lineIndex];
                var btns = (line["buttons"] as JArray) ?? new JArray();
                if (btns.Count == 0) return; // 安全檢查

                var curBtn = (JObject)btns[0];
                string curAddr = curBtn.Value<string>("address") ?? "";
                if (!string.Equals(curAddr, pressedAddress, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"ℹ️ 收到 {pressedAddress}，但目前應按 {curAddr}（忽略）");
                    return;
                }

                // 視覺：完成 → POST（綠）
                ForceState(curAddr, "POST");
                Log($"✅ 完成：{curAddr}");

                // 跳到下一個 line（可能還是按鈕，或已經到 LED）
                _lineIndex++;
                await ActivateFromCurrentLineAsync();
                return;
            }

            // LED 階段：只吃 1008
            if (!string.Equals(pressedAddress, LedSwitchButton, StringComparison.OrdinalIgnoreCase))
            {
                Log($"ℹ️ LED 階段收到了 {pressedAddress}，僅 {LedSwitchButton} 有效（忽略）。");
                return;
            }

            // 防抖 + 互斥
            if (_handling1008) { Log("↩️ 1008 處理中，略過重複事件"); return; }
            _handling1008 = true;

            try
            {
                var now = DateTime.Now;
                if ((now - _last1008At) < TimeSpan.FromMilliseconds(400))
                {
                    Log("↩️ 1008 連續回報（已防抖略過）");
                    return;
                }
                _last1008At = now;

                // 視覺：1008 → POST（綠），稍待回 PRE
                ForceState(LedSwitchButton, "POST");

                // 跳到下一個「LED line」
                _lineIndex++;
                while (_lines != null && _lineIndex < _lines.Count)
                {
                    var ln = (JObject)_lines[_lineIndex];
                    var steps = (ln["ledSteps"] as JArray) ?? new JArray();
                    if (steps.Count > 0) break; // 找到下一段 LED
                    _lineIndex++; //（理論上不會遇到非 LED，但保險）
                }

                if (_lines == null || _lineIndex >= _lines.Count)
                {
                    // LED 全部完成 → 結單
                    await Task.Delay(150);
                    ForceState(LedSwitchButton, "PRE");
                    await FinishOrderAsync();
                    return;
                }

                await Task.Delay(150);
                ForceState(LedSwitchButton, "PRE");
                await ActivateFromCurrentLineAsync(); // 顯示下一段 LED（同一函式會進 LED）
            }
            finally
            {
                _handling1008 = false;
            }
        }

        // ========= 使用「目前 line」的單一 LED 段 =========
        private async Task TriggerCurrentLedLineAsync()
        {
            if (_lines == null || _lineIndex < 0 || _lineIndex >= _lines.Count) return;

            var line = (JObject)_lines[_lineIndex];
            string hubIp = line.Value<string>("hubIp") ?? LedHubIpDefault;
            var steps = (line["ledSteps"] as JArray) ?? new JArray();
            if (steps.Count == 0) return;

            var step = (JObject)steps[0]; // 每個 LED line 只有一段
            string orderId = step.Value<string>("orderId") ?? "";
            int qty = step.Value<int?>("qty") ?? 0;

            // 1008 顯示數量（藍）
            ShowQtyOn1008(qty);

            // 送 LED Hub：只送此段 orderId
            await PostLedOrderIdAsync(hubIp, orderId);

            var part = line.Value<string>("partNo") ?? "-";
            var loc = line.Value<string>("location") ?? "-";
            Log($"➡️ LED Line#{_lineIndex + 1}：part={part} loc={loc} orderId={orderId} qty={qty}（等待按 {LedSwitchButton} 切換）");
        }

        private void ShowQtyOn1008(int qty)
        {
            ForceState(LedSwitchButton, "PRE");
            SendToHub(LedSwitchButton, "SET_PARAMETER", "PRE_BUTTON_MODE",
                $"ENABLED//COLBLUE/SOLID_RING/@{qty}@@");
            Log($"[BTN1008] 顯示數量 @{qty}@（藍）");
        }

        private async Task PostLedOrderIdAsync(string hubIp, string orderId)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var json = new JObject { ["orderId"] = orderId }.ToString();
                var url = $"http://{hubIp}:8080/orders";
                var resp = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                Log($"[LED→API] POST {url} ({(int)resp.StatusCode}) orderId={orderId}");
            }
            catch (Exception ex)
            {
                Log("❌ LED API 送 orderId 失敗：" + ex.Message);
            }
        }

        // ========= 整單結束：全滅 + 等待下一張 =========
        private async Task FinishOrderAsync()
        {
            try
            {
                await PostLedOrderIdAsync(LedHubIpDefault, "L1-OFF");
                await PostLedOrderIdAsync(LedHubIpDefault, "L2-OFF");
            }
            catch (Exception ex)
            {
                Log("⚠️ LED 全滅例外：" + ex.Message);
            }

            ClearAllButtonsVisuals();

            Log("🎉 工單已完成（全部燈與按鈕熄滅）。");
            Log("👉 請按『Get Order』載入下一張工單，再按『Start』開始。");

            // 重置狀態
            _currentOrder = null;
            _lines = null;
            _lineIndex = -1;
            _inLedPhase = false;
        }

        private async Task EnsureAllOffAsync()
        {
            try
            {
                await PostLedOrderIdAsync(LedHubIpDefault, "L1-OFF");
                await PostLedOrderIdAsync(LedHubIpDefault, "L2-OFF");
            }
            catch (Exception ex)
            {
                Log("⚠️ 啟動全滅(LED) 例外：" + ex.Message);
            }

            ClearAllButtonsVisuals();
        }

        // ====== SmartCAP 指令封裝 / Polling ======
        private void EnablePolling(string list)
        {
            if (_svc == null) return;
            _svc.Send(new MessageToSensor
            {
                Address = "HUB",
                CommandType = "SET_PARAMETER",
                Offset = "",
                Payload = list
            });
            Log($"🔁 啟用輪詢名單：{list}");
        }

        private void ForceState(string address, string mode)
        {
            if (_svc == null) return;
            var cmd = mode.Equals("POST", StringComparison.OrdinalIgnoreCase)
                      ? "POST_PRESSED_STATE" : "PRE_PRESSED_STATE";
            _svc.Send(new MessageToSensor
            {
                Address = string.IsNullOrWhiteSpace(address) ? defaultAddress : address,
                CommandType = "SET_STATUS",
                Offset = "SPECIAL_COMMANDS",
                Payload = cmd
            });
            Log($"[SDK→Send] {address} | SET_STATUS | SPECIAL_COMMANDS | {cmd}");
        }

        private void SendToHub(string addr, string cmd, string offset, string payload)
        {
            if (_svc == null) { Log("⚠️ 尚未連線 HUB"); return; }
            _svc.Send(new MessageToSensor
            {
                Address = addr,
                CommandType = cmd,
                Offset = offset,
                Payload = payload
            });
            Log($"[SDK→Send] {addr} | {cmd} | {offset} | {payload}");
        }

        private void ClearAllButtonsVisuals()
        {
            // 將 PRE / POST 的顏色都設為 COLOFF（不亮）
            SendToHub(defaultAddress, "SET_PARAMETER", "PRE_BUTTON_MODE", "ENABLED//COLOFF/SOLID_RING/@@@@");
            SendToHub(defaultAddress, "SET_PARAMETER", "POST_BUTTON_MODE", "ENABLED//COLOFF/SOLID_RING/@@@@");
            // 回到 PRE 狀態
            ForceState(defaultAddress, "PRE");
            SendToHub(defaultAddress, "SET_PARAMETER", "PRE_BUTTON_MODE", "ENABLED//COLOFF/SOLID_RING/@@@@");
            Log("🧽 已清空全部按鈕外觀（PRE/POST=COLOFF），回到 PRE。");
        }

        // ====== 小工具 ======
        private static string WrapAtIfNeeded(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "@@";
            if (!s.StartsWith("@")) s = "@" + s;
            if (!s.EndsWith("@")) s += "@";
            return s;
        }

        private static string GetEffectiveAddress(string addrField, string payload)
        {
            if (Regex.IsMatch(addrField ?? "", @"^\d{4}$")) return addrField;
            var m = Regex.Match(payload ?? "", @"\b(\d{4})\b");
            return m.Success ? m.Groups[1].Value : addrField;
        }

        private void Log(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            if (lstLog.InvokeRequired)
                lstLog.Invoke((Action)(() => lstLog.Items.Add(line)));
            else
                lstLog.Items.Add(line);
        }

        private void UpdateHubStatus(string text)
        {
            if (lblHubStatus.InvokeRequired)
                lblHubStatus.Invoke((Action)(() => lblHubStatus.Text = text));
            else
                lblHubStatus.Text = text;
        }

        // ====== 四張工單（3 個按鈕 line + 3 個 LED line） ======
        private string BuildEmbeddedOrdersJson()
        {
            return @"
[
  {
    ""orderId"": ""WO-001"",
    ""hubIp"": ""10.0.60.96"",
    ""lines"": [
      { ""partNo"": ""CY-3891A-A01"", ""qty"": 31, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1003"", ""color"": ""COLGREEN"", ""text"": ""@31@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""CY-37G1Y-A01"", ""qty"": 22, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1005"", ""color"": ""COLGREEN"", ""text"": ""@22@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""DH-3195-Z01"", ""qty"": 13, ""location"": ""B4G13"", ""buttons"": [ { ""address"": ""1012"", ""color"": ""COLGREEN"", ""text"": ""@13@"" } ], ""ledSteps"": [] },

      { ""partNo"": ""LF-3192Y-A01"", ""qty"": 3, ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L1-011"", ""qty"": 3 } ] },
      { ""partNo"": ""LV-3197-A01"", ""qty"": 8,  ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L1-007"", ""qty"": 8 } ] },
      { ""partNo"": ""LV-3199-A01"", ""qty"": 4,  ""location"": ""B4G11"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L2-003"", ""qty"": 4 } ] }
    ]
  },
  {
    ""orderId"": ""WO-002"",
    ""hubIp"": ""10.0.60.96"",
    ""lines"": [
      { ""partNo"": ""CS-3415Y-A00"", ""qty"": 52, ""location"": ""B4G13"", ""buttons"": [ { ""address"": ""1010"", ""color"": ""COLGREEN"", ""text"": ""@52@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""LV-3191-A00"", ""qty"": 32, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1007"", ""color"": ""COLGREEN"", ""text"": ""@32@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""CY-37G1Y-A01"", ""qty"": 15, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1005"", ""color"": ""COLGREEN"", ""text"": ""@15@"" } ], ""ledSteps"": [] },

      { ""partNo"": ""CY-3795-A02"", ""qty"": 2, ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L1-002"", ""qty"": 2 } ] },
      { ""partNo"": ""LV-3895Y-A01"", ""qty"": 7,  ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L1-005"", ""qty"": 7 } ] },
      { ""partNo"": ""LX3691AY-A00"", ""qty"": 3,  ""location"": ""B4G11"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L2-011"", ""qty"": 3 } ] }
    ]
  },
  {
    ""orderId"": ""WO-003"",
    ""hubIp"": ""10.0.60.96"",
    ""lines"": [
      { ""partNo"": ""CS-34A8-A02"", ""qty"": 89, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1002"", ""color"": ""COLGREEN"", ""text"": ""@89@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""DH-3195-Z01"", ""qty"": 74, ""location"": ""B4G13"", ""buttons"": [ { ""address"": ""1012"", ""color"": ""COLGREEN"", ""text"": ""@74@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""CS-3415Y-A00"", ""qty"": 55, ""location"": ""B4G13"", ""buttons"": [ { ""address"": ""1010"", ""color"": ""COLGREEN"", ""text"": ""@55@"" } ], ""ledSteps"": [] },

      { ""partNo"": ""LA-3089-A00"", ""qty"": 1, ""location"": ""B4G11"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L2-005"", ""qty"":  1 } ] },
      { ""partNo"": ""CY-3795-A02"", ""qty"": 6,  ""location"": ""B4G11"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L2-009"", ""qty"": 6 } ] },
      { ""partNo"": ""LA-3016-A00"", ""qty"": 5,  ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L1-012"", ""qty"": 5 } ] }
    ]
  },
  {
    ""orderId"": ""WO-004"",
    ""hubIp"": ""10.0.60.96"",
    ""lines"": [
      { ""partNo"": ""MAT-D01"", ""qty"": 35, ""location"": ""B4G13"", ""buttons"": [ { ""address"": ""1009"", ""color"": ""COLGREEN"", ""text"": ""@35@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""MAT-D02"", ""qty"": 75, ""location"": ""B4G13"", ""buttons"": [ { ""address"": ""1013"", ""color"": ""COLGREEN"", ""text"": ""@75@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""MAT-D03"", ""qty"": 66, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1001"", ""color"": ""COLGREEN"", ""text"": ""@66@"" } ], ""ledSteps"": [] },

      { ""partNo"": ""LX-3691AY-A00"", ""qty"": 9,  ""location"": ""B4G11"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L2-011"", ""qty"": 9 } ] },
      { ""partNo"": ""DB-30B2-Z00"", ""qty"": 7,  ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L1-010"", ""qty"": 7 } ] },
      { ""partNo"": ""LV-3898-A01"", ""qty"": 2,  ""location"": ""B4G12"", ""buttons"": [], ""ledSteps"": [ { ""orderId"": ""L2-004"", ""qty"": 2 } ] }
    ]
  }
]
";
        }
    }
}
