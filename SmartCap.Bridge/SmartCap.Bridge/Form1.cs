// SmartCap.Bridge / Form_E2.cs
// SIM (No HUB / No MQTT / No real HTTP)
// E2 scenario: Data format error -> HTTP 400 Bad Request
// Run 20 events: always trigger 400 -> simulate fix time -> retry OK -> record dt
// At the end: SaveFileDialog export CSV, then popup MessageBox (Title=ERROR, Content=SUMMARY)

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

        // ===== Order mock (same structure) =====
        private JArray? _orderQueue;
        private JObject? _currentOrder;
        private JArray? _lines;
        private int _lineIndex = -1;

        // ===== E2 config / metrics =====
        private const string E2_TITLE = "ERROR";
        private const string E2_KEY = "BAD_REQUEST_400";

        // Fixed: trigger 400 events exactly N times
        private const int EVENTS = 20;

        // SIM fix time range
        private const int FIX_TIME_MIN_MS = 2000;
        private const int FIX_TIME_MAX_MS = 15000;

        private int _seq = 0;
        private readonly Dictionary<string, DateTime> _start = new();

        private int _ok = 0;
        private int _fail = 0;

        private readonly List<long> _timesMs = new();
        private long _maxMs = 0;
        private long _minMs = 0;

        private int _badCount = 0;
        private int _retryCount = 0;

        private readonly List<E2Record> _records = new();

        private class E2Record
        {
            public int Index { get; set; }
            public string Type { get; set; } = "";
            public string Id { get; set; } = "";
            public string Reason { get; set; } = "";
            public DateTime T0 { get; set; }
            public DateTime T1 { get; set; }
            public long DtMs { get; set; }
            public string Result { get; set; } = ""; // PASS / FAIL
        }

        private class E2BadRequestException : Exception
        {
            public string Reason { get; }
            public E2BadRequestException(string reason) : base(reason) { Reason = reason; }
        }

        private readonly Random _rng = new Random();

        public Form1()
        {
            InitializeComponent();

            // Keep UI the same (Designer not changed)
            btnConnectHub.Click += btnConnectHub_Click;
            btnGetOrder.Click += btnGetOrder_Click;
            btnStart.Click += btnStart_Click;

            UpdateHubStatus("HUB: Not connected (SIM)");
            Log("[INFO] SIM MODE (E2)");
        }

        // ================== UI: Connect (SIM) ==================
        private async void btnConnectHub_Click(object? sender, EventArgs e)
        {
            if (_running) { Log("[INFO] running"); return; }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            UpdateHubStatus("Connecting... (SIM)");
            try
            {
                await Task.Delay(250, _cts.Token);
                string ip = SafeText(txtHubIp, "10.0.60.96");
                UpdateHubStatus("HUB Connected (SIM)");
                Log($"[INFO] connected (SIM) ip={ip}");
            }
            catch (OperationCanceledException)
            {
                UpdateHubStatus("HUB: Not connected (SIM)");
                Log("[INFO] connect canceled");
            }
        }

        // ================== UI: Get Order (SIM) ==================
        private void btnGetOrder_Click(object? sender, EventArgs e)
        {
            Log("[UI] Get Order (SIM)");

            try
            {
                // Show one "bad 400" example then show one "fixed" example
                string bad = BuildBadPayloadGuaranteed();
                Log($"[DEBUG][HTTP] payload={TrimOneLine(bad, 140)}");

                try
                {
                    ValidateOrderPayloadOrThrowE2(bad);
                    Log("[WARN] bad payload unexpectedly valid");
                }
                catch (E2BadRequestException ex)
                {
                    Log($"[HTTP][400] reason={ex.Reason}");
                }

                string fixedPayload = BuildEmbeddedValidOrdersJson();
                ValidateOrderPayloadOrThrowE2(fixedPayload);
                LoadSingleOrderFromPayload(fixedPayload);
                Log("[OK] fixed payload example loaded (SIM)");
            }
            catch (Exception ex)
            {
                Log("[ERR] " + ex.Message);
            }
        }

        // ================== UI: Start (SIM) ==================
        private async void btnStart_Click(object? sender, EventArgs e)
        {
            if (_running) { Log("[INFO] running"); return; }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _running = true;

            try
            {
                ResetE2Metrics();

                Log($"[TEST] E2 start events={EVENTS} fix_time={FIX_TIME_MIN_MS}~{FIX_TIME_MAX_MS}ms");

                for (int i = 1; i <= EVENTS; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    Log($"[TEST] ---- {i}/{EVENTS} ----");

                    // 1) always trigger bad payload -> 400
                    string badPayload = BuildBadPayloadGuaranteed();
                    Log($"[DEBUG][HTTP] GET /orders/current -> payload={TrimOneLine(badPayload, 140)}");

                    try
                    {
                        ValidateOrderPayloadOrThrowE2(badPayload);

                        // should never be here
                        Log("[WARN] bad payload validated as OK (unexpected)");
                        await Task.Delay(50, ct);
                        continue;
                    }
                    catch (E2BadRequestException ex)
                    {
                        _badCount++;
                        Log($"[HTTP][400] reason={ex.Reason}");

                        // 2) timer start
                        string id = StartTimer(E2_KEY);

                        // 3) simulate fix time
                        int fixMs = _rng.Next(FIX_TIME_MIN_MS, FIX_TIME_MAX_MS + 1);
                        await Task.Delay(fixMs, ct);

                        // 4) retry with valid payload
                        _retryCount++;
                        string fixedPayload = BuildEmbeddedValidOrdersJson();
                        Log($"[DEBUG][HTTP] Retry -> payload={TrimOneLine(fixedPayload, 140)}");

                        try
                        {
                            ValidateOrderPayloadOrThrowE2(fixedPayload);
                            LoadSingleOrderFromPayload(fixedPayload);

                            StopTimerOk(id, E2_KEY, ex.Reason);
                        }
                        catch (E2BadRequestException ex2)
                        {
                            StopTimerFail(id, E2_KEY, "Retry still 400: " + ex2.Reason);
                        }
                    }

                    await Task.Delay(80, ct);
                }

                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E2_KEY);

                MessageBox.Show(
                    summaryText,
                    E2_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (OperationCanceledException)
            {
                Log("[INFO] canceled");
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E2_KEY);

                MessageBox.Show(
                    summaryText,
                    E2_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                Log("[ERR] " + ex.Message);
                string summaryText = SummaryAndReturnText();

                SaveCsvWithDialog(E2_KEY);

                MessageBox.Show(
                    summaryText + Environment.NewLine + Environment.NewLine + "Exception: " + ex.Message,
                    E2_TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                _running = false;
            }
        }

        // ================== E2: Payload validation ==================
        private void ValidateOrderPayloadOrThrowE2(string payloadJson)
        {
            try
            {
                var t = payloadJson.Trim();
                JToken root;

                if (t.StartsWith("["))
                    root = JArray.Parse(t);
                else
                    root = JObject.Parse(t);

                JObject order;

                if (root is JArray arr)
                {
                    if (arr.Count == 0) throw new E2BadRequestException("payload array empty");
                    order = arr[0] as JObject ?? throw new E2BadRequestException("payload[0] not object");
                }
                else
                {
                    order = root as JObject ?? throw new E2BadRequestException("payload not object");
                }

                var orderId = order.Value<string>("orderId");
                if (string.IsNullOrWhiteSpace(orderId))
                    throw new E2BadRequestException("orderId missing/empty");

                var lines = order["lines"] as JArray;
                if (lines == null)
                    throw new E2BadRequestException("lines missing or not array");
                if (lines.Count == 0)
                    throw new E2BadRequestException("lines empty");

                for (int i = 0; i < lines.Count; i++)
                {
                    var ln = lines[i] as JObject;
                    if (ln == null) throw new E2BadRequestException($"lines[{i}] not object");

                    var partNo = ln.Value<string>("partNo");
                    if (string.IsNullOrWhiteSpace(partNo))
                        throw new E2BadRequestException($"lines[{i}].partNo missing/empty");

                    var qtyTok = ln["qty"];
                    if (qtyTok == null || (qtyTok.Type != JTokenType.Integer && qtyTok.Type != JTokenType.Float))
                        throw new E2BadRequestException($"lines[{i}].qty missing/not number");

                    int qty = ln.Value<int?>("qty") ?? 0;
                    if (qty <= 0)
                        throw new E2BadRequestException($"lines[{i}].qty <= 0");

                    var loc = ln.Value<string>("location");
                    if (string.IsNullOrWhiteSpace(loc))
                        throw new E2BadRequestException($"lines[{i}].location missing/empty");
                }
            }
            catch (E2BadRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new E2BadRequestException("payload parse error: " + ex.Message);
            }
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

        // ================== E2 metrics helpers ==================
        private void ResetE2Metrics()
        {
            _seq = 0;
            _start.Clear();

            _ok = 0;
            _fail = 0;

            _timesMs.Clear();
            _maxMs = 0;
            _minMs = 0;

            _badCount = 0;
            _retryCount = 0;

            _records.Clear();
        }

        private string StartTimer(string type)
        {
            string id = $"E2-{++_seq:0000}";
            var t0 = DateTime.Now;
            _start[id] = t0;
            Log($"[E2] START id={id} type={type}");
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

            _records.Add(new E2Record
            {
                Index = _records.Count + 1,
                Type = type,
                Id = id,
                Reason = reason,
                T0 = t0,
                T1 = t1,
                DtMs = dt,
                Result = "PASS"
            });

            Log($"[E2] DONE id={id} PASS dt={dt}ms");
            _start.Remove(id);
        }

        private void StopTimerFail(string id, string type, string reason)
        {
            var t1 = DateTime.Now;
            if (!_start.TryGetValue(id, out var t0)) t0 = t1;
            long dt = (long)(t1 - t0).TotalMilliseconds;

            _fail++;

            _records.Add(new E2Record
            {
                Index = _records.Count + 1,
                Type = type,
                Id = id,
                Reason = reason,
                T0 = t0,
                T1 = t1,
                DtMs = dt,
                Result = "FAIL"
            });

            Log($"[E2] DONE id={id} FAIL dt={dt}ms");
            _start.Remove(id);
        }

        private void SaveCsvWithDialog(string errorType)
        {
            try
            {
                // For E2 events=20, records should be 20 even if failures happen.
                if (_records.Count == 0)
                {
                    Log("[INFO] no records, skip csv");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Export CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"e2_{errorType}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    Log("[INFO] csv canceled");
                    return;
                }

                var lines = new List<string> { "index,type,id,result,reason,t0,t1,dt_ms" };

                foreach (var r in _records)
                {
                    var safeReason = (r.Reason ?? "").Replace(",", " ");
                    lines.Add($"{r.Index},{r.Type},{r.Id},{r.Result},{safeReason},{r.T0:HH:mm:ss.fff},{r.T1:HH:mm:ss.fff},{r.DtMs}");
                }

                File.WriteAllLines(sfd.FileName, lines, Encoding.UTF8);
                Log("[INFO] csv saved");
            }
            catch (Exception ex)
            {
                Log("[ERR] csv save: " + ex.Message);
            }
        }

        private string SummaryAndReturnText()
        {
            // total = events (fixed 20) => here by actual recorded events:
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

            var sb = new StringBuilder();
            sb.AppendLine("[SUMMARY]");
            sb.AppendLine(E2_KEY);
            sb.AppendLine();
            sb.AppendLine($"total={total}");
            sb.AppendLine($"success={_ok}");
            sb.AppendLine($"fail={_fail}");
            sb.AppendLine($"success_rate={successRate:0.00}%");
            sb.AppendLine();
            sb.AppendLine($"avg_fix_time={avgS:0.0}s");
            sb.AppendLine($"min_fix_time={minS:0.0}s");
            sb.AppendLine($"max_fix_time={maxS:0.0}s");

            // optional: keep internal counters (not shown in summary text)
            Log("[SUMMARY]");
            Log(E2_KEY);
            Log($"total={total}");
            Log($"success={_ok}");
            Log($"fail={_fail}");
            Log($"success_rate={successRate:0.00}%");
            Log($"avg_fix_time={avgS:0.0}s");
            Log($"min_fix_time={minS:0.0}s");
            Log($"max_fix_time={maxS:0.0}s");

            return sb.ToString();
        }

        // ================== SIM payloads ==================
        private string BuildBadPayloadGuaranteed()
        {
            // guaranteed 400: qty=0
            return @"
{
  ""orderId"": ""WO-001"",
  ""hubIp"": ""10.0.60.96"",
  ""lines"": [
    { ""partNo"": ""CY-3891A-A01"", ""qty"": 0, ""location"": ""B4G14"" }
  ]
}";
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
