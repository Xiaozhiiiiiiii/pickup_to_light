// SmartCap.Bridge / Form_E5.cs
// SIM (No HUB / No MQTT / No real HTTP)
// E5 scenario: State Inconsistency (ERP state != HUB/Local state) while comm is OK
// Requirement: record 20 "E5 events" (each event triggers a mismatch), auto reconcile (SIM delay), measure recovery time
// Recovery time (dt) = from mismatch detected (t0) -> states consistent again (t1)
// At the end: SaveFileDialog export CSV, then popup MessageBox (Title = ERROR, Content = SUMMARY)
//
// Notes:
// - UI controls assumed existing (same as E1/E2/E4): btnConnectHub, btnGetOrder, btnStart, txtHubIp, lblHubStatus, lstLog
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

        // ===== E5 constants =====
        private const string E5_TITLE = "ERROR";
        private const string E5_KEY = "STATE_MISMATCH";
        private const int EVENTS = 20;

        // ---- SIM knobs (adjust by your real observation) ----
        // Typical reconciliation time (seconds) - adjust as needed
        private const int RECONCILE_MIN_MS = 800;    // 0.8s
        private const int RECONCILE_MAX_MS = 8000;   // 8s

        // Small processing jitter
        private const int HANDLE_JITTER_MIN_MS = 80;
        private const int HANDLE_JITTER_MAX_MS = 250;

        // ===== Metrics =====
        private int _seq = 0;
        private readonly Dictionary<string, DateTime> _start = new();

        private int _ok = 0, _fail = 0;
        private readonly List<long> _timesMs = new();
        private long _maxMs = 0;
        private long _minMs = 0;

        private int _mismatchCount = 0;
        private int _reconcileCount = 0;

        private readonly List<E5Record> _records = new();

        private class E5Record
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
            Log("🧪 SIM MODE：本版本做 E5（狀態不同步 / ERP != HUB）模擬，不連線 HUB。");
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
                Log("🔇 [SIM] 連線完成：E5 不測 HTTP，不測 HUB 重啟，只測『狀態不同步』與自動對齊。");
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] Connect canceled");
                UpdateHubStatus("HUB: Not connected (SIM)");
            }
        }

        // ================== UI: Get Order (SIM) ==================
        // For E5, GetOrder just loads a valid order (same style as others) and prints it.
        private void btnGetOrder_Click(object? sender, EventArgs e)
        {
            Log("🟦 [UI] Get Order (SIM / E5)");

            try
            {
                string payloadJson = BuildEmbeddedValidOrdersJson();
                Log($"[DEBUG][ORDER] payload={TrimOneLine(payloadJson, 140)}");

                LoadSingleOrderFromPayload(payloadJson);

                var oid = _currentOrder?.Value<string>("orderId") ?? "WO-001";
                var cnt = _lines?.Count ?? 0;

                Log($"[INFO][ORDER] Order {oid} loaded (SIM)");
                Log($"✅ [SIM] 工單 {oid}，共 {cnt} 個 line");

                Log("👉 [SIM] 按下『Start』開始：將執行 20 次 E5（狀態不同步）測試並統計恢復時間。");
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
                ResetE5Metrics();

                Log("🟩 [UI] Start (SIM / E5)");
                Log($"[INFO][TEST] Scenario=E5({E5_KEY}) events={EVENTS}");
                Log($"[INFO][TEST] reconcile_delay={RECONCILE_MIN_MS}~{RECONCILE_MAX_MS}ms");

                for (int round = 1; round <= EVENTS; round++)
                {
                    ct.ThrowIfCancellationRequested();

                    Log($"[INFO][TEST] ---- Round {round}/{EVENTS} ----");

                    // Ensure we have an order loaded (SIM)
                    if (_currentOrder == null || _lines == null)
                    {
                        LoadSingleOrderFromPayload(BuildEmbeddedValidOrdersJson());
                    }

                    // 1) Create a mismatch scenario (always mismatch for "E5 events")
                    var scenario = SimCreateMismatchScenario(round);
                    Log($"[DEBUG][STATE] ERP={scenario.ErpState} HUB={scenario.HubState} orderId={scenario.OrderId}");

                    // 2) Detect mismatch -> start timer
                    string id = StartTimer(E5_KEY);
                    _mismatchCount++;

                    // Optional small jitter to mimic handling
                    int jitter = _rng.Next(HANDLE_JITTER_MIN_MS, HANDLE_JITTER_MAX_MS + 1);
                    await Task.Delay(jitter, ct);

                    // 3) Reconcile (SIM): "re-query / align / replay"
                    int reconcileMs = _rng.Next(RECONCILE_MIN_MS, RECONCILE_MAX_MS + 1);
                    Log($"[ACTION][E5] Reconciling states... (SIM) {reconcileMs}ms");
                    await Task.Delay(reconcileMs, ct);

                    // 4) After reconcile, states become consistent (SIM)
                    _reconcileCount++;
                    var aligned = scenario.Align();

                    // 5) Verify aligned
                    if (aligned.IsConsistent)
                    {
                        Log($"[OK  ][E5] Aligned ERP={aligned.ErpState} HUB={aligned.HubState}");
                        StopTimerOk(id, E5_KEY, scenario.Reason);
                    }
                    else
                    {
                        Log("[FAIL][E5] Alignment failed (SIM)");
                        StopTimerFail(id, E5_KEY, "Alignment failed");
                    }

                    await Task.Delay(120, ct);
                }

                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E5_KEY);

                MessageBox.Show(
                    summaryText,
                    E5_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ [SIM] 已停止（Cancel）");
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E5_KEY);

                MessageBox.Show(
                    summaryText,
                    E5_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log("❌ [SIM] 例外：" + ex.Message);
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E5_KEY);

                MessageBox.Show(
                    summaryText + Environment.NewLine + Environment.NewLine + "Exception: " + ex.Message,
                    E5_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _running = false;
            }
        }

        // ================== E5 model ==================

        private enum WorkState
        {
            Unknown = 0,
            Pending,
            InProgress,
            Completed,
            Canceled
        }

        private class MismatchScenario
        {
            public string OrderId { get; set; } = "WO-001";
            public WorkState ErpState { get; set; } = WorkState.InProgress;
            public WorkState HubState { get; set; } = WorkState.Completed;
            public string Reason { get; set; } = "ERP-HUB state mismatch";

            public bool IsConsistent => ErpState == HubState;

            // SIM reconcile policy:
            // - If ERP says Completed but HUB says InProgress => treat HUB as source-of-truth for physical progress (align ERP to HUB) OR vice versa.
            // To keep it stable and explainable, we choose:
            //   If either side is Completed, align to the "more conservative" state = InProgress (avoid premature completion).
            public MismatchScenario Align()
            {
                if (ErpState == HubState) return this;

                // Conservative align:
                // Completed vs InProgress => InProgress
                if ((ErpState == WorkState.Completed && HubState == WorkState.InProgress) ||
                    (ErpState == WorkState.InProgress && HubState == WorkState.Completed))
                {
                    ErpState = WorkState.InProgress;
                    HubState = WorkState.InProgress;
                    return this;
                }

                // Pending vs InProgress => InProgress (continue workflow)
                if ((ErpState == WorkState.Pending && HubState == WorkState.InProgress) ||
                    (ErpState == WorkState.InProgress && HubState == WorkState.Pending))
                {
                    ErpState = WorkState.InProgress;
                    HubState = WorkState.InProgress;
                    return this;
                }

                // Completed vs Pending => InProgress (safe middle)
                if ((ErpState == WorkState.Completed && HubState == WorkState.Pending) ||
                    (ErpState == WorkState.Pending && HubState == WorkState.Completed))
                {
                    ErpState = WorkState.InProgress;
                    HubState = WorkState.InProgress;
                    return this;
                }

                // Default: align HUB to ERP (or ERP to HUB) — here pick ERP as final if uncertain
                HubState = ErpState;
                return this;
            }
        }

        private MismatchScenario SimCreateMismatchScenario(int round)
        {
            var oid = _currentOrder?.Value<string>("orderId") ?? "WO-001";
            var s = new MismatchScenario { OrderId = oid };

            // Always create mismatch for "E5 events"
            // Rotate a few realistic variants
            int variant = (round % 4);
            switch (variant)
            {
                case 0:
                    s.ErpState = WorkState.Completed;
                    s.HubState = WorkState.InProgress;
                    s.Reason = "ERP=Completed but HUB still InProgress";
                    break;
                case 1:
                    s.ErpState = WorkState.InProgress;
                    s.HubState = WorkState.Completed;
                    s.Reason = "HUB=Completed but ERP still InProgress";
                    break;
                case 2:
                    s.ErpState = WorkState.Pending;
                    s.HubState = WorkState.InProgress;
                    s.Reason = "ERP=Pending but HUB already InProgress";
                    break;
                default:
                    s.ErpState = WorkState.Completed;
                    s.HubState = WorkState.Pending;
                    s.Reason = "ERP=Completed but HUB Pending (stale state)";
                    break;
            }

            return s;
        }

        // ================== Metrics ==================

        private void ResetE5Metrics()
        {
            _seq = 0;
            _start.Clear();

            _ok = 0;
            _fail = 0;

            _timesMs.Clear();
            _maxMs = 0;
            _minMs = 0;

            _mismatchCount = 0;
            _reconcileCount = 0;

            _records.Clear();
        }

        private string StartTimer(string type)
        {
            string id = $"E5-{++_seq:0000}";
            var t0 = DateTime.Now;
            _start[id] = t0;
            Log($"[WARN][E5] START id={id} type={type} t0={t0:HH:mm:ss.fff}");
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

            _records.Add(new E5Record
            {
                Index = _records.Count + 1,
                Type = type,
                Id = id,
                Reason = reason,
                T0 = t0,
                T1 = t1,
                DtMs = dt
            });

            Log($"[OK  ][E5] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms reason={reason}");
            _start.Remove(id);
        }

        private void StopTimerFail(string id, string type, string reason)
        {
            var t1 = DateTime.Now;
            if (!_start.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            _fail++;
            Log($"[FAIL][E5] DONE  id={id} type={type} t1={t1:HH:mm:ss.fff} dt={dt}ms reason={reason}");
            _start.Remove(id);
        }

        private void SaveCsvWithDialog(string errorType)
        {
            try
            {
                if (_records.Count == 0)
                {
                    Log("⚠️ 無 E5 記錄資料，略過 CSV 輸出");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Export E5 CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"e5_{errorType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
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
                Log($"📄 E5 CSV 已儲存：{sfd.FileName}");
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
            Log(E5_KEY);
            Log($"total={total}");
            Log($"success={_ok}");
            Log($"fail={_fail}");
            Log($"success_rate={successRate:0.00}%");
            Log($"avg_fix_time={avgS:0.0}s");
            Log($"min_fix_time={minS:0.0}s");
            Log($"max_fix_time={maxS:0.0}s");

            var sb = new StringBuilder();
            sb.AppendLine("[SUMMARY]");
            sb.AppendLine(E5_KEY);
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

        // ================== Order payload helpers (same style as E2/E4) ==================

        private void LoadSingleOrderFromPayload(string payloadJson)
        {
            var json = payloadJson.Trim();
            if (!json.StartsWith("[")) json = "[" + json + "]";
            _orderQueue = JArray.Parse(json);

            _currentOrder = (JObject)_orderQueue[0];
            _lines = (JArray)_currentOrder["lines"]!;
            _lineIndex = -1;
        }

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

        // ================== Small utilities (same style as E2/E4) ==================

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
