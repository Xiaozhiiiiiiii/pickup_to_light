// SmartCap.Bridge / Form_E4.cs
// SIM (No HUB / No MQTT / No real HTTP)
// E4 scenario: ERP internal error -> HTTP 500 Internal Server Error
// Requirement: record 20 "500 events" (each event triggers 500), then retry after a delay until success (SIM: one retry success).
// Recovery time (dt) = from 500 detected (t0) -> retry success (t1)
// At the end: SaveFileDialog export CSV, then popup MessageBox (Title = ERROR, Content = SUMMARY)
//
// Notes:
// - UI controls assumed existing (same as E1/E2): btnConnectHub, btnGetOrder, btnStart, txtHubIp, lblHubStatus, lstLog
// - No extra UI controls required.

using System;
using System.Collections.Generic;
using System.IO;
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

        // ===== Order mock placeholders (keep structure consistent) =====
        private JArray? _orderQueue;
        private JObject? _currentOrder;
        private JArray? _lines;
        private int _lineIndex = -1;

        // ===== E4 constants =====
        private const string E4_TITLE = "ERROR";
        private const string E4_KEY = "INTERNAL_SERVER_ERROR_500";
        private const int EVENTS = 20;

        // ---- SIM knobs (adjust by your real observation) ----
        // Typical "ERP recover time" (seconds) - adjust as needed
        private const int RECOVERY_MIN_MS = 2000;   // 2s
        private const int RECOVERY_MAX_MS = 15000;  // 15s

        // Small handling jitter (UI/log realistic)
        private const int HANDLE_JITTER_MIN_MS = 80;
        private const int HANDLE_JITTER_MAX_MS = 250;

        // ===== Metrics =====
        private int _seq = 0;
        private readonly Dictionary<string, DateTime> _start = new();

        private int _ok = 0, _fail = 0;
        private readonly List<long> _timesMs = new();
        private long _maxMs = 0;
        private long _minMs = 0;

        private int _err500Count = 0;
        private int _retryCount = 0;

        private readonly List<E4Record> _records = new();

        private class E4Record
        {
            public int Index { get; set; }
            public string Type { get; set; } = "";
            public string Id { get; set; } = "";
            public string Reason { get; set; } = "";
            public DateTime T0 { get; set; }
            public DateTime T1 { get; set; }
            public long DtMs { get; set; }
        }

        // ===== Random =====
        private readonly Random _rng = new Random();

        public Form1()
        {
            InitializeComponent();

            // Keep UI the same (Designer not changed)
            btnConnectHub.Click += btnConnectHub_Click;
            btnGetOrder.Click += btnGetOrder_Click;
            btnStart.Click += btnStart_Click;

            UpdateHubStatus("HUB: Not connected (SIM)");
            Log("🧪 SIM MODE：本版本做 E4（HTTP 500 / ERP 內部錯誤）模擬，不連線 HUB。");
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

                Log("🔇 [SIM] 連線完成：E4 不測 HUB 重啟，只測 ERP 500 + 重試恢復。");
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] Connect canceled");
                UpdateHubStatus("HUB: Not connected (SIM)");
            }
        }

        // ================== UI: Get Order (SIM) ==================
        // For E4, GetOrder demonstrates what "500 internal error" looks like and why we should not proceed.
        private void btnGetOrder_Click(object? sender, EventArgs e)
        {
            Log("🟦 [UI] Get Order (SIM / E4)");

            try
            {
                string payloadJson = SimHttpGetOrder500PayloadJson();
                Log($"[DEBUG][HTTP] GET /orders/current -> status=500 payload={TrimOneLine(payloadJson, 140)}");

                // Throw as E4
                ThrowE4Internal("ERP internal error (SIM)");

            }
            catch (E4InternalException ex)
            {
                Log($"[HTTP][500] Internal Server Error (SIM) reason={ex.Reason}");
                Log("✅ [E4] 已拒絕執行（不啟動流程、不亮燈、不進 line），等待 ERP 恢復後重試。");
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

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _running = true;

            try
            {
                ResetE4Metrics();

                Log("🟩 [UI] Start (SIM / E4)");
                Log($"[INFO][TEST] Scenario=E4({E4_KEY}) events={EVENTS}");
                Log($"[INFO][TEST] recovery_delay={RECOVERY_MIN_MS}~{RECOVERY_MAX_MS}ms");

                for (int round = 1; round <= EVENTS; round++)
                {
                    ct.ThrowIfCancellationRequested();

                    Log($"[INFO][TEST] ---- Round {round}/{EVENTS} ----");

                    // 1) Call API -> always 500 for E4 event
                    string payloadJson = SimHttpGetOrder500PayloadJson();
                    Log($"[DEBUG][HTTP] GET /orders/current -> payload={TrimOneLine(payloadJson, 140)}");

                    // 2) Start timer at 500 detected
                    string id = StartTimer(E4_KEY);

                    try
                    {
                        int jitter = _rng.Next(HANDLE_JITTER_MIN_MS, HANDLE_JITTER_MAX_MS + 1);
                        await Task.Delay(jitter, ct);

                        // Always raise 500
                        ThrowE4Internal(ExtractE4ReasonFromPayload(payloadJson));
                    }
                    catch (E4InternalException ex)
                    {
                        _err500Count++;
                        Log($"[HTTP][500] Internal Server Error (SIM) reason={ex.Reason}");

                        // 3) Retry after "ERP recovery time"
                        int recoverMs = _rng.Next(RECOVERY_MIN_MS, RECOVERY_MAX_MS + 1);
                        Log($"[ACTION][E4] Waiting ERP recover... (SIM) {recoverMs}ms");
                        await Task.Delay(recoverMs, ct);

                        // 4) Retry once (SIM): guaranteed success payload
                        _retryCount++;
                        string okPayload = BuildEmbeddedValidOrdersJson();
                        Log($"[ACTION][E4] Retry GET /orders/current -> payload={TrimOneLine(okPayload, 140)}");

                        try
                        {
                            ValidateOrderPayloadOrThrowParse(okPayload);
                            LoadSingleOrderFromPayload(okPayload);

                            Log("[OK  ][E4] Retry success (ERP recovered)");
                            StopTimerOk(id, E4_KEY, ex.Reason);
                        }
                        catch (Exception ex2)
                        {
                            Log($"[FAIL][E4] Retry still failed: {ex2.Message}");
                            StopTimerFail(id, E4_KEY, "Retry failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("❌ [E4] Unexpected exception: " + ex.Message);
                        StopTimerFail(id, E4_KEY, "Exception: " + ex.Message);
                    }

                    await Task.Delay(120, ct);
                }

                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E4_KEY);

                MessageBox.Show(
                    summaryText,
                    E4_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] 已停止（Cancel）");
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E4_KEY);

                MessageBox.Show(
                    summaryText,
                    E4_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log("❌ [SIM] 例外：" + ex.Message);
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E4_KEY);

                MessageBox.Show(
                    summaryText + Environment.NewLine + Environment.NewLine + "Exception: " + ex.Message,
                    E4_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _running = false;
            }
        }

        // ================== E4 core ==================

        private class E4InternalException : Exception
        {
            public string Reason { get; }
            public E4InternalException(string reason) : base(reason) { Reason = reason; }
        }

        private static void ThrowE4Internal(string reason)
        {
            throw new E4InternalException(reason);
        }

        private static string ExtractE4ReasonFromPayload(string payloadJson)
        {
            try
            {
                var t = payloadJson.Trim();
                if (t.StartsWith("{"))
                {
                    var obj = JObject.Parse(t);
                    var msg = obj.Value<string>("message");
                    if (!string.IsNullOrWhiteSpace(msg)) return msg!;
                    var err = obj.Value<string>("error");
                    if (!string.IsNullOrWhiteSpace(err)) return err!;
                }
            }
            catch { }
            return "ERP internal error";
        }

        // E4 retry success needs payload parse; keep minimal check (avoid 400/404 logic here)
        private static void ValidateOrderPayloadOrThrowParse(string payloadJson)
        {
            // If parse fails, treat as unexpected error for E4 retry.
            var t = payloadJson.Trim();
            JToken root;
            if (t.StartsWith("[")) root = JArray.Parse(t);
            else root = JObject.Parse(t);

            JObject order;
            if (root is JArray arr)
            {
                if (arr.Count == 0) throw new Exception("payload array empty");
                order = arr[0] as JObject ?? throw new Exception("payload[0] not object");
            }
            else
            {
                order = root as JObject ?? throw new Exception("payload not object");
            }

            var orderId = order.Value<string>("orderId");
            if (string.IsNullOrWhiteSpace(orderId)) throw new Exception("orderId missing/empty");

            var lines = order["lines"] as JArray;
            if (lines == null || lines.Count == 0) throw new Exception("lines missing/empty");
        }

        private void LoadSingleOrderFromPayload(string payloadJson)
        {
            var json = payloadJson.Trim();
            if (!json.StartsWith("[")) json = "[" + json + "]";
            _orderQueue = JArray.Parse(json);

            _currentOrder = (JObject)_orderQueue[0];
            _lines = (JArray)_currentOrder["lines"]!;
            _lineIndex = -1;
        }

        // ================== Metrics ==================

        private void ResetE4Metrics()
        {
            _seq = 0;
            _start.Clear();

            _ok = 0;
            _fail = 0;

            _timesMs.Clear();
            _maxMs = 0;
            _minMs = 0;

            _err500Count = 0;
            _retryCount = 0;

            _records.Clear();
        }

        private string StartTimer(string type)
        {
            string id = $"E4-{++_seq:0000}";
            var t0 = DateTime.Now;
            _start[id] = t0;
            Log($"[WARN][E4] START id={id} type={type} t0={t0:HH:mm:ss.fff}");
            return id;
        }

        private void StopTimerOk(string id, string type, string reason)
        {
            var t1 = DateTime.Now;
            if (!_start.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            _ok++;
            _timesMs.Add(dt);

            if (_minMs == 0 || dt < _minMs) _minMs = dt;
            if (dt > _maxMs) _maxMs = dt;

            _records.Add(new E4Record
            {
                Index = _records.Count + 1,
                Type = type,
                Id = id,
                Reason = reason,
                T0 = t0,
                T1 = t1,
                DtMs = dt
            });

            Log($"[OK  ][E4] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms reason={reason}");
            _start.Remove(id);
        }

        private void StopTimerFail(string id, string type, string reason)
        {
            var t1 = DateTime.Now;
            if (!_start.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            _fail++;
            Log($"[FAIL][E4] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms reason={reason}");
            _start.Remove(id);
        }

        private void SaveCsvWithDialog(string errorType)
        {
            try
            {
                if (_records.Count == 0)
                {
                    Log("⚠️ 無 E4 記錄資料，略過 CSV 輸出");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Export E4 CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"e4_{errorType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    Log("ℹ️ 使用者取消 CSV 儲存");
                    return;
                }

                var lines = new List<string> { "index,type,id,reason,t0,t1,dt_ms" };

                foreach (var r in _records)
                {
                    var safeReason = (r.Reason ?? "").Replace(",", " ");
                    lines.Add($"{r.Index},{r.Type},{r.Id},{safeReason},{r.T0:HH:mm:ss.fff},{r.T1:HH:mm:ss.fff},{r.DtMs}");
                }

                File.WriteAllLines(sfd.FileName, lines, Encoding.UTF8);
                Log($"📄 E4 CSV 已儲存：{sfd.FileName}");
            }
            catch (Exception ex)
            {
                Log("❌ CSV 儲存失敗：" + ex.Message);
            }
        }

        private string SummaryAndReturnText()
        {
            int total = _ok + _fail;
            double successRate = total > 0 ? (double)_ok / total * 100.0 : 0.0;

            double avgMs = 0;
            if (_timesMs.Count > 0)
            {
                long sum = 0;
                for (int i = 0; i < _timesMs.Count; i++) sum += _timesMs[i];
                avgMs = (double)sum / _timesMs.Count;
            }

            double avgS = avgMs / 1000.0;
            double minS = _minMs > 0 ? _minMs / 1000.0 : 0.0;
            double maxS = _maxMs > 0 ? _maxMs / 1000.0 : 0.0;

            Log("[SUMMARY]");
            Log(E4_KEY);
            Log($"total={total}");
            Log($"success={_ok}");
            Log($"fail={_fail}");
            Log($"success_rate={successRate:0.00}%");
            Log($"avg_fix_time={avgS:0.0}s");
            Log($"min_fix_time={minS:0.0}s");
            Log($"max_fix_time={maxS:0.0}s");

            var sb = new StringBuilder();
            sb.AppendLine("[SUMMARY]");
            sb.AppendLine(E4_KEY);
            sb.AppendLine();
            sb.AppendLine($"total={total}");
            sb.AppendLine($"success={_ok}");
            sb.AppendLine($"fail={_fail}");
            sb.AppendLine($"success_rate={successRate:0.00}%");
            sb.AppendLine();
            sb.AppendLine($"avg_fix_time={avgS:0.0}s");
            sb.AppendLine($"min_fix_time={minS:0.0}s");
            sb.AppendLine($"max_fix_time={maxS:0.0}s");

            return sb.ToString();
        }

        // ================== SIM payloads ==================

        private static string SimHttpGetOrder500PayloadJson()
        {
            return @"
{
  ""status"": 500,
  ""error"": ""Internal Server Error"",
  ""message"": ""ERP service exception""
}";
        }

        // Same embedded valid order as E2 (safe to parse + keep structure)
        private string BuildEmbeddedValidOrdersJson()
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

        // ================== Small utilities (same style as E2) ==================

        private static string SafeText(TextBox? tb, string fallback)
        {
            try
            {
                var s = tb?.Text?.Trim();
                return string.IsNullOrWhiteSpace(s) ? fallback : s!;
            }
            catch { return fallback; }
        }

        private static string TrimOneLine(string s, int maxLen)
        {
            if (s == null) return "";
            var one = s.Replace("\r", " ").Replace("\n", " ").Trim();
            if (one.Length <= maxLen) return one;
            return one.Substring(0, maxLen) + "...";
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
    }
}
