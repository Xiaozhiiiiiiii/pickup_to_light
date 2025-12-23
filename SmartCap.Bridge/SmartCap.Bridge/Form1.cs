// SmartCap.Bridge / Form_E3.cs
// SIM (No HUB / No MQTT / No real HTTP)
// E3 scenario: Task not found -> HTTP 404 Not Found
// Run 20 events: always trigger 404 -> system must reject (no LED action) -> record handling time
// At the end: SaveFileDialog export CSV, then popup MessageBox (Title=ERROR, Content=SUMMARY)
//
// Notes:
// - Keep log density similar to E2: each event logs request, response, reason, decision, timing.
// - E3 differs from E2: NO "fix payload" retry. It should be rejected immediately.

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

        // ===== Order mock placeholders (keep structure consistent with E1/E2) =====
        private JArray? _orderQueue;
        private JObject? _currentOrder;
        private JArray? _lines;
        private int _lineIndex = -1;

        // ===== E3 constants =====
        private const string E3_TITLE = "ERROR";
        private const string E3_KEY = "NOT_FOUND_404";
        private const int EVENTS = 20;

        // optional small jitter to simulate handling/network latency
        private const int HANDLE_JITTER_MIN_MS = 80;
        private const int HANDLE_JITTER_MAX_MS = 350;

        // ===== Metrics =====
        private int _seq = 0;
        private readonly Dictionary<string, DateTime> _start = new();

        private int _ok = 0;   // correct reject
        private int _fail = 0; // wrong behavior / unexpected parse, etc.

        private readonly List<long> _timesMs = new();
        private long _maxMs = 0;
        private long _minMs = 0;

        private int _notFoundCount = 0;

        private readonly List<E3Record> _records = new();

        private class E3Record
        {
            public int Index { get; set; }
            public string Type { get; set; } = "";
            public string Id { get; set; } = "";
            public string OrderId { get; set; } = "";
            public string Reason { get; set; } = "";
            public DateTime T0 { get; set; }
            public DateTime T1 { get; set; }
            public long DtMs { get; set; }
            public string Result { get; set; } = ""; // PASS / FAIL
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
            Log("🧪 SIM MODE：本版本做 E3（HTTP 404 / 任務不存在）模擬，不連線 HUB。");
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
                Log("🔇 [SIM] 連線完成：E3 不測 HUB 重啟，只測 Order API 404。");
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
            Log("🟦 [UI] Get Order (SIM / E3)");

            try
            {
                // E3 shows: query a task that doesn't exist -> 404
                var orderId = "WO-404";

                string payloadJson = SimHttpGetOrder404PayloadJson(orderId);
                Log($"[DEBUG][HTTP] GET /orders/{orderId} -> status=404 payload={TrimOneLine(payloadJson, 140)}");

                // For E3, we treat 404 as "Not Found" immediately, no side effects.
                ThrowE3NotFound(orderId, "orderId not found");

            }
            catch (E3NotFoundException ex)
            {
                Log($"[HTTP][404] Not Found (SIM) orderId={ex.OrderId} reason={ex.Reason}");
                Log("✅ [E3] 已拒絕執行（不啟動流程、不亮燈、不進 line）。");
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
                ResetE3Metrics();

                Log("🟩 [UI] Start (SIM / E3)");
                Log($"[INFO][TEST] Scenario=E3({E3_KEY}) events={EVENTS}");

                for (int i = 1; i <= EVENTS; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    Log($"[INFO][TEST] ---- Round {i}/{EVENTS} ----");

                    // Always trigger 404
                    string orderId = $"WO-{i:000}-404";
                    string payloadJson = SimHttpGetOrder404PayloadJson(orderId);

                    Log($"[DEBUG][HTTP] GET /orders/{orderId} -> payload={TrimOneLine(payloadJson, 140)}");

                    // E3 handling time = detection + decision (no retry/fix)
                    string id = StartTimer(E3_KEY);

                    try
                    {
                        // Simulate request/handling jitter
                        int jitter = _rng.Next(HANDLE_JITTER_MIN_MS, HANDLE_JITTER_MAX_MS + 1);
                        await Task.Delay(jitter, ct);

                        // E3: raise "not found" and reject
                        ThrowE3NotFound(orderId, ExtractE3ReasonFromPayload(payloadJson));

                        // Should not reach
                        Log("[WARN][E3] Unexpected: not found not thrown");
                        StopTimerFail(id, E3_KEY, orderId, "Unexpected pass");
                    }
                    catch (E3NotFoundException ex)
                    {
                        _notFoundCount++;
                        Log($"[HTTP][404] Not Found (SIM) orderId={ex.OrderId} reason={ex.Reason}");
                        Log("✅ [E3] Reject: no action performed (SIM)");

                        StopTimerOk(id, E3_KEY, ex.OrderId, ex.Reason);
                    }
                    catch (Exception ex)
                    {
                        Log("❌ [E3] Unexpected exception: " + ex.Message);
                        StopTimerFail(id, E3_KEY, orderId, "Exception: " + ex.Message);
                    }

                    await Task.Delay(100, ct);
                }

                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E3_KEY);

                MessageBox.Show(
                    summaryText,
                    E3_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] 已停止（Cancel）");
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E3_KEY);

                MessageBox.Show(
                    summaryText,
                    E3_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log("❌ [SIM] 例外：" + ex.Message);
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E3_KEY);

                MessageBox.Show(
                    summaryText + Environment.NewLine + Environment.NewLine + "Exception: " + ex.Message,
                    E3_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _running = false;
            }
        }

        // ================== E3 core ==================

        private class E3NotFoundException : Exception
        {
            public string OrderId { get; }
            public string Reason { get; }
            public E3NotFoundException(string orderId, string reason) : base(reason)
            {
                OrderId = orderId;
                Reason = reason;
            }
        }

        private static void ThrowE3NotFound(string orderId, string reason)
        {
            throw new E3NotFoundException(orderId, reason);
        }

        private static string ExtractE3ReasonFromPayload(string payloadJson)
        {
            // Keep simple & safe: try read message field, else return generic.
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
            return "task not found";
        }

        // ================== Metrics ==================

        private void ResetE3Metrics()
        {
            _seq = 0;
            _start.Clear();

            _ok = 0;
            _fail = 0;

            _timesMs.Clear();
            _maxMs = 0;
            _minMs = 0;

            _notFoundCount = 0;
            _records.Clear();
        }

        private string StartTimer(string type)
        {
            string id = $"E3-{++_seq:0000}";
            var t0 = DateTime.Now;
            _start[id] = t0;
            Log($"[WARN][E3] START id={id} type={type} t0={t0:HH:mm:ss.fff}");
            return id;
        }

        private void StopTimerOk(string id, string type, string orderId, string reason)
        {
            var t1 = DateTime.Now;
            if (!_start.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            _ok++;
            _timesMs.Add(dt);

            if (_minMs == 0 || dt < _minMs) _minMs = dt;
            if (dt > _maxMs) _maxMs = dt;

            _records.Add(new E3Record
            {
                Index = _records.Count + 1,
                Type = type,
                Id = id,
                OrderId = orderId,
                Reason = reason,
                T0 = t0,
                T1 = t1,
                DtMs = dt,
                Result = "PASS"
            });

            Log($"[OK  ][E3] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms orderId={orderId}");
            _start.Remove(id);
        }

        private void StopTimerFail(string id, string type, string orderId, string reason)
        {
            var t1 = DateTime.Now;
            if (!_start.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            _fail++;
            _records.Add(new E3Record
            {
                Index = _records.Count + 1,
                Type = type,
                Id = id,
                OrderId = orderId,
                Reason = reason,
                T0 = t0,
                T1 = t1,
                DtMs = dt,
                Result = "FAIL"
            });

            Log($"[FAIL][E3] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms orderId={orderId} reason={reason}");
            _start.Remove(id);
        }

        private void SaveCsvWithDialog(string errorType)
        {
            try
            {
                if (_records.Count == 0)
                {
                    Log("⚠️ 無 E3 記錄資料，略過 CSV 輸出");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Export E3 CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"e3_{errorType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    Log("ℹ️ 使用者取消 CSV 儲存");
                    return;
                }

                var lines = new List<string> { "index,type,id,result,orderId,reason,t0,t1,dt_ms" };

                foreach (var r in _records)
                {
                    var safeReason = (r.Reason ?? "").Replace(",", " ");
                    lines.Add($"{r.Index},{r.Type},{r.Id},{r.Result},{r.OrderId},{safeReason},{r.T0:HH:mm:ss.fff},{r.T1:HH:mm:ss.fff},{r.DtMs}");
                }

                File.WriteAllLines(sfd.FileName, lines, Encoding.UTF8);
                Log($"📄 E3 CSV 已儲存：{sfd.FileName}");
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
            Log(E3_KEY);
            Log($"total={total}");
            Log($"success={_ok}");
            Log($"fail={_fail}");
            Log($"success_rate={successRate:0.00}%");
            Log($"avg_fix_time={avgS:0.0}s");
            Log($"min_fix_time={minS:0.0}s");
            Log($"max_fix_time={maxS:0.0}s");

            var sb = new StringBuilder();
            sb.AppendLine("[SUMMARY]");
            sb.AppendLine(E3_KEY);
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

        // ================== SIM: 404 payload generator ==================
        private static string SimHttpGetOrder404PayloadJson(string orderId)
        {
            // Typical ERP error response (SIM)
            return $@"
{{
  ""status"": 404,
  ""error"": ""Not Found"",
  ""message"": ""orderId '{orderId}' not found""
}}";
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
