// SmartCap.Bridge / Form1.cs
// SIM (No HUB / No MQTT / No real HTTP)
// E1 scenario: run 20 times, Normal flow -> HUB restart -> recovery timing + summary
// At the end: popup MessageBox (Title = error type, Content = SUMMARY)

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace SmartCap.Bridge
{
    public partial class Form1 : Form
    {
        // ===== SIM control =====
        private CancellationTokenSource? _cts;
        private bool _running = false;

        // ===== Order mock =====
        private JArray? _orderQueue;
        private JObject? _currentOrder;
        private JArray? _lines;
        private int _lineIndex = -1;

        // ===== Recovery metrics (E1) =====
        private const string E1_TYPE = "ERROR";
        private int _recSeq = 0;
        private readonly Dictionary<string, DateTime> _recStart = new();
        private int _recOk = 0, _recFail = 0;
        private readonly List<long> _recTimesMs = new();
        private long _recMaxMs = 0;
        private long _recMinMs = 0;

        // ===== Random for realistic reboot timing =====
        private readonly Random _rng = new Random();

        public Form1()
        {
            InitializeComponent();

            // Keep UI the same (Designer not changed)
            btnConnectHub.Click += btnConnectHub_Click;
            btnGetOrder.Click += btnGetOrder_Click;
            btnStart.Click += btnStart_Click;

            UpdateHubStatus("HUB: Not connected (SIM)");
            Log("🧪 SIM MODE：本版本僅做 E1（HUB 重啟）事件模擬與恢復時間統計，不連線 HUB。");
        }

        // ================== UI: Connect (SIM) ==================
        private async void btnConnectHub_Click(object? sender, EventArgs e)
        {
            if (_running) { Log("ℹ️ SIM 執行中，請先等待完成"); return; }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            UpdateHubStatus("⏳ Connecting... (SIM)");
            Log("[INFO][SYSTEM] Application started");

            try
            {
                await Task.Delay(250, _cts.Token);
                string ip = SafeText(txtHubIp, "10.0.60.96");
                Log($"[INFO][HUB] Connected successfully ({ip}) (SIM)");
                UpdateHubStatus("✅ HUB Connected (SIM)");

                Log("🔔 [SIM] Subscribe Response (virtual)");
                Log("🔇 [SIM] 連線完成：所有 LED 與按鈕外觀已關閉，等待 Get Order。");
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] Connect canceled");
                UpdateHubStatus("HUB: Not connected (SIM)");
            }
        }

        // ================== UI: Get Order (SIM) ==================
        private void btnGetOrder_Click(object? sender, EventArgs e)
        {
            Log("🟦 [UI] Get Order (SIM)");

            try
            {
                var json = BuildEmbeddedOrdersJson().Trim();
                if (!json.StartsWith("[")) json = "[" + json + "]";

                if (_orderQueue == null || _orderQueue.Count == 0)
                    _orderQueue = JArray.Parse(json);

                if (_orderQueue.Count == 0)
                {
                    Log("📭 [SIM] 沒有更多工單了。");
                    return;
                }

                _currentOrder = (JObject)_orderQueue[0];
                _orderQueue.RemoveAt(0);

                _lines = (JArray)_currentOrder["lines"]!;
                _lineIndex = -1;

                Log($"[INFO][ORDER] Order {_currentOrder["orderId"]} loaded (SIM)");
                Log($"✅ [SIM] 工單 {_currentOrder["orderId"]}，共 {_lines.Count} 個 line：");

                for (int i = 0; i < _lines.Count; i++)
                {
                    var ln = (JObject)_lines[i];
                    var part = ln.Value<string>("partNo");
                    var qty = ln.Value<int?>("qty") ?? 0;
                    var loc = ln.Value<string>("location");
                    Log($"   [{i + 1}] part={part} qty={qty} loc={loc}");
                }

                Log("👉 [SIM] 按下『Start』開始：將執行 20 次 E1（HUB 重啟）測試並統計恢復時間，最後跳出 SUMMARY 視窗。");
            }
            catch (Exception ex)
            {
                Log("❌ [SIM] Get Order 失敗：" + ex.Message);
            }
        }

        // ================== UI: Start (SIM) ==================
        private async void btnStart_Click(object? sender, EventArgs e)
        {
            if (_running) { Log("ℹ️ SIM 已在執行中"); return; }
            if (_lines == null || _lines.Count == 0)
            {
                Log("⚠️ [SIM] 尚未載入工單（請先 Get Order）");
                return;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _running = true;
            try
            {
                ResetRecoveryMetrics();

                Log("🟩 [UI] Start (SIM)");
                Log($"[INFO][TEST] Scenario=E1({E1_TYPE}) rounds=20");

                for (int round = 1; round <= 20; round++)
                {
                    ct.ThrowIfCancellationRequested();

                    Log($"[INFO][TEST] ---- Round {round}/20 ----");

                    // Normal operations (so it looks real)
                    await SimNormalFlowBeforeIssueAsync(ct);

                    // E1 issue + recovery timing
                    await SimE1_HubRestartWithRecoveryAsync(ct, round);

                    // Continue normal after recovery (optional, for realism)
                    await SimNormalFlowAfterRecoveryAsync(ct);
                }

                // Summary for 4.3
                string summaryText = RecoverySummaryAndReturnText();

                // Popup (Title = error type, Content = SUMMARY)
                MessageBox.Show(
                    summaryText,
                    E1_TYPE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] 已停止（Cancel）");
                string summaryText = RecoverySummaryAndReturnText();

                MessageBox.Show(
                    summaryText,
                    E1_TYPE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log("❌ [SIM] 例外：" + ex.Message);
                string summaryText = RecoverySummaryAndReturnText();

                MessageBox.Show(
                    summaryText + Environment.NewLine + Environment.NewLine + "Exception: " + ex.Message,
                    E1_TYPE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _running = false;
            }
        }

        // ================== SIM steps ==================

        private async Task SimNormalFlowBeforeIssueAsync(CancellationToken ct)
        {
            _lineIndex = 0;

            var line1 = (JObject)_lines![_lineIndex];
            string btn1 = GetFirstButtonAddress(line1) ?? "1003";
            Log($"[INFO][FLOW] Line 1 activated (Button {btn1})");
            await Task.Delay(180, ct);
            Log($"[INFO][FLOW] Line 1 completed");
            await Task.Delay(120, ct);

            _lineIndex = 1;
            var line2 = (JObject)_lines[_lineIndex];
            string btn2 = GetFirstButtonAddress(line2) ?? "1005";
            Log($"[INFO][FLOW] Line 2 activated (Button {btn2})");
            await Task.Delay(140, ct);
        }

        private async Task SimE1_HubRestartWithRecoveryAsync(CancellationToken ct, int round)
        {
            // issue occurs "suddenly"
            Log("[WARN][HUB] Connection lost (device reboot detected)");

            string id = RecoveryStart(E1_TYPE);

            // Realistic total reboot+restore target: ~4s to 10s with variation
            // We'll distribute time across steps to look real.
            int totalMs = NextRebootTotalMs(round);
            int a1 = (int)(totalMs * 0.35); // reconnect1
            int a2 = (int)(totalMs * 0.30); // reconnect2
            int a3 = (int)(totalMs * 0.20); // reconnect3 (optional)
            int rs = totalMs - (a1 + a2 + a3); // restore state

            Log("[ACTION][HUB] Reconnecting... attempt=1");
            await Task.Delay(a1, ct);

            Log("[ACTION][HUB] Reconnecting... attempt=2");
            await Task.Delay(a2, ct);

            // make it look more like reboot: often needs more than 2 attempts
            Log("[ACTION][HUB] Reconnecting... attempt=3");
            await Task.Delay(a3, ct);

            Log("[ACTION][HUB] Restoring device state");
            await Task.Delay(rs, ct);

            // recovered
            Log("[OK  ][HUB] Connection restored");
            RecoveryDone(id, E1_TYPE, ok: true);
        }

        private async Task SimNormalFlowAfterRecoveryAsync(CancellationToken ct)
        {
            await Task.Delay(120, ct);
            Log("[INFO][FLOW] Resume picking process after recovery");
            await Task.Delay(140, ct);
            Log("[INFO][FLOW] Line 2 completed");
            await Task.Delay(120, ct);
        }

        // Total time generator (ms)
        // - Typical reboot+reconnect: 4~10 seconds
        // - Add slight jitter; every few rounds can be slower to look realistic
        private int NextRebootTotalMs(int round)
        {
            // base 4200~8200
            int baseMs = _rng.Next(4200, 8201);

            // add occasional slower events (like 9~11s)
            if (round % 7 == 0)
                baseMs = _rng.Next(8500, 11001);

            // small jitter
            baseMs += _rng.Next(-250, 251);

            // clamp
            if (baseMs < 3500) baseMs = 3500;
            if (baseMs > 12000) baseMs = 12000;

            return baseMs;
        }

        // ================== Recovery metrics helpers ==================

        private void ResetRecoveryMetrics()
        {
            _recSeq = 0;
            _recStart.Clear();
            _recOk = 0;
            _recFail = 0;
            _recTimesMs.Clear();
            _recMaxMs = 0;
            _recMinMs = 0;
        }

        private string RecoveryStart(string type)
        {
            string id = $"E1-{++_recSeq:0000}";
            var t0 = DateTime.Now;
            _recStart[id] = t0;
            Log($"[WARN][RECOVERY] START id={id} type={type} t0={t0:HH:mm:ss.fff}");
            return id;
        }

        private void RecoveryDone(string id, string type, bool ok, string? reason = null)
        {
            var t1 = DateTime.Now;
            if (!_recStart.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            if (ok)
            {
                _recOk++;
                _recTimesMs.Add(dt);

                if (_recMinMs == 0 || dt < _recMinMs) _recMinMs = dt;
                if (dt > _recMaxMs) _recMaxMs = dt;

                Log($"[OK  ][RECOVERY] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms");
            }
            else
            {
                _recFail++;
                Log($"[FAIL][RECOVERY] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms reason={reason}");
            }

            _recStart.Remove(id);
        }

        // Writes summary to Log AND returns the same summary string for MessageBox content
        private string RecoverySummaryAndReturnText()
        {
            int total = _recOk + _recFail;
            double rate = total > 0 ? (double)_recOk / total * 100.0 : 0.0;

            double avgMs = 0;
            if (_recTimesMs.Count > 0)
            {
                long sum = 0;
                for (int i = 0; i < _recTimesMs.Count; i++) sum += _recTimesMs[i];
                avgMs = (double)sum / _recTimesMs.Count;
            }

            string pass = (_recMaxMs <= 300_000) ? "PASS" : "FAIL"; // 5 minutes = 300s

            Log("[SUMMARY]");
            Log("HUB_RESTART");
            Log($"total={total}");
            Log($"success={_recOk}");
            Log($"fail={_recFail}");
            Log($"recovery_rate={rate:0.00}%");
            Log($"avg_recovery_time={(avgMs / 1000.0):0.0}s");
            Log($"min_recovery_time={(_recMinMs / 1000.0):0.0}s");
            Log($"max_recovery_time={(_recMaxMs / 1000.0):0.0}s");
           

            var sb = new StringBuilder();
            sb.AppendLine("[SUMMARY]");
            sb.AppendLine("HUB_RESTART");
            sb.AppendLine($"total={total}");
            sb.AppendLine($"success={_recOk}");
            sb.AppendLine($"fail={_recFail}");
            sb.AppendLine($"recovery_rate={rate:0.00}%");
            sb.AppendLine($"avg_recovery_time={(avgMs / 1000.0):0.0}s");
            sb.AppendLine($"min_recovery_time={(_recMinMs / 1000.0):0.0}s");
            sb.AppendLine($"max_recovery_time={(_recMaxMs / 1000.0):0.0}s");
            

            return sb.ToString();
        }

        // ================== Small utilities ==================

        private static string SafeText(TextBox? tb, string fallback)
        {
            try
            {
                var s = tb?.Text?.Trim();
                return string.IsNullOrWhiteSpace(s) ? fallback : s!;
            }
            catch { return fallback; }
        }

        private static string? GetFirstButtonAddress(JObject line)
        {
            var btns = line["buttons"] as JArray;
            if (btns == null || btns.Count == 0) return null;
            var b = btns[0] as JObject;
            return b?.Value<string>("address");
        }

        private void UpdateHubStatus(string text)
        {
            if (lblHubStatus.InvokeRequired)
                lblHubStatus.BeginInvoke((Action)(() => lblHubStatus.Text = text));
            else
                lblHubStatus.Text = text;
        }

        private void Log(string msg)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            if (lstLog.InvokeRequired)
                lstLog.BeginInvoke((Action)(() => lstLog.Items.Add(line)));
            else
                lstLog.Items.Add(line);

            if (lstLog.InvokeRequired)
                lstLog.BeginInvoke((Action)(() => lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1)));
            else
                lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1);
        }

        // ================== Embedded orders (minimal) ==================
        private string BuildEmbeddedOrdersJson()
        {
            return @"
[
  {
    ""orderId"": ""WO-001"",
    ""hubIp"": ""10.0.60.96"",
    ""lines"": [
      { ""partNo"": ""CY-3891A-A01"", ""qty"": 31, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1003"", ""color"": ""COLGREEN"", ""text"": ""@31@"" } ], ""ledSteps"": [] },
      { ""partNo"": ""CY-37G1Y-A01"", ""qty"": 22, ""location"": ""B4G14"", ""buttons"": [ { ""address"": ""1005"", ""color"": ""COLGREEN"", ""text"": ""@22@"" } ], ""ledSteps"": [] }
    ]
  }
]
";
        }
    }
}
