using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace SmartCap.Bridge
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource? _cts;
        private bool _running = false;

        // ===== KPI Criteria (for screenshot / review) =====
        private const int RequiredSamples = 200;
        private const double RequiredSuccessRate = 99.0; // %
        private const long RequiredMaxLatencyMs = 500;    // <= 0.5 sec

        // ✅ 每筆固定 Delay（你要求）
        private const int PerRequestDelayMs = 350;

        // 統計
        private int _sent = 0, _ok = 0, _fail = 0;
        private long _okLatencySumMs = 0, _maxLatencyMs = 0;
        private readonly List<long> _latencies = new List<long>();

        // Scenario text (2.1.1)
        private const string ScenarioTitle =
            "";

        public Form1()
        {
            InitializeComponent();

            // UI header for screenshot (Designer 要有 lblScenario / lblCriteria)
            lblScenario.Text = ScenarioTitle;
            lblCriteria.Text =
                $"";

            btnRun.Click += btnRun_Click;
            btnStop.Click += btnStop_Click;
            btnExportCsv.Click += btnExportCsv_Click;

            btnStop.Enabled = false;
            btnExportCsv.Enabled = false;

            UpdateKpi();
        }

        private async void btnRun_Click(object? sender, EventArgs e)
        {
            if (_running) return;

            string hubIp = txtHubIp.Text.Trim();
            string orderId = txtOrderId.Text.Trim();

            int count = (int)numCount.Value;
            int intervalMs = (int)numIntervalMs.Value;
            int timeoutMs = (int)numTimeoutMs.Value;

            if (string.IsNullOrWhiteSpace(hubIp))
            {
                Log("⚠️ Hub IP 為空");
                return;
            }
            if (string.IsNullOrWhiteSpace(orderId))
            {
                Log("⚠️ orderId 為空");
                return;
            }

            if (count < RequiredSamples)
            {
                Log($"⚠️ 測試筆數建議至少 {RequiredSamples}（目前={count}）以符合審查條件");
            }

            ResetStats();

            _running = true;
            btnRun.Enabled = false;
            btnStop.Enabled = true;
            btnExportCsv.Enabled = false;

            _cts = new CancellationTokenSource();

            Log($"🧪 START | {ScenarioTitle}");
            Log($"hub={hubIp} orderId={orderId} count={count} interval={intervalMs}ms timeout={timeoutMs}ms");
            Log("✅ 成功定義：HTTP 2xx");

            try
            {
                for (int i = 1; i <= count; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    string cmdId = $"CMD-{DateTime.Now:HHmmss}-{i:D3}";

                    _sent++;

                    // CONTROL log
                    Log($"[CTRL] cmd={cmdId} action=SendOrder orderId={orderId} hub={hubIp}");

                    var (ok, ms, httpCode) = await PostOrderTimedAsync(hubIp, orderId, timeoutMs, _cts.Token);

                    // ✅ 單一反應時間：HTTP + 固定 delay
                    long latency = ms + PerRequestDelayMs;

                    if (ok)
                    {
                        _ok++;
                        _okLatencySumMs += latency;
                        _latencies.Add(latency);
                        if (latency > _maxLatencyMs) _maxLatencyMs = latency;
                    }
                    else
                    {
                        _fail++;
                    }

                    UpdateKpi();

                    // VERIFY log
                    string verifyResult = ok ? "PASS" : "FAIL";
                    Log($"[VERIFY] cmd={cmdId} rule=HTTP_2XX result={verifyResult} " +
                        $"code={(httpCode == 0 ? "ERR" : httpCode.ToString())} Response Time={latency}ms");

                    if (!ok)
                    {
                        Log($"[ERROR] cmd={cmdId} verification failed (timeout/reject/non-2xx)");
                    }

                    
                    // ✅ 每筆固定 Delay 350ms（你要求）
                    await Task.Delay(PerRequestDelayMs, _cts.Token);

                    // 原本 interval（保留）
                    if (intervalMs > 0)
                        await Task.Delay(intervalMs, _cts.Token);
                }

                string kpiText = BuildKpiText();
                Log("✅ DONE | " + kpiText);
                btnExportCsv.Enabled = _latencies.Count > 0;

                ShowSummaryPopup();
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ 已停止（Stop）");
                btnExportCsv.Enabled = _latencies.Count > 0;

                ShowSummaryPopup();
            }
            catch (Exception ex)
            {
                Log("❌ 例外：" + ex.Message);

                // 也給一個摘要（方便截圖）
                ShowSummaryPopup();
            }
            finally
            {
                _running = false;
                btnRun.Enabled = true;
                btnStop.Enabled = false;

                _cts?.Dispose();
                _cts = null;
            }
        }

        private void btnStop_Click(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private void btnExportCsv_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_latencies.Count == 0)
                {
                    Log("⚠️ 沒有可輸出的延遲資料");
                    return;
                }

                using var sfd = new SaveFileDialog
                {
                    Title = "Export Latency CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"c3_stress_2_1_1_mqtt_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                File.WriteAllLines(sfd.FileName, _latencies.Select((ms, idx) => $"{idx + 1},{ms}"), Encoding.UTF8);
                Log($"📄 CSV 已輸出：{sfd.FileName}");
            }
            catch (Exception ex)
            {
                Log("❌ CSV 輸出失敗：" + ex.Message);
            }
        }

        private void ResetStats()
        {
            _sent = _ok = _fail = 0;
            _okLatencySumMs = 0;
            _maxLatencyMs = 0;
            _latencies.Clear();
            lstLog.Items.Clear();
            UpdateKpi();
        }

        private string BuildKpiText()
        {
            double rate = _sent > 0 ? (double)_ok / _sent * 100.0 : 0;
            double avg = _ok > 0 ? (double)_okLatencySumMs / _ok : 0;

            // 未滿 200 筆時：RUNNING（不顯示 FAIL）
            if (_sent < RequiredSamples)
            {
                return $"[RUNNING] Total={_sent}/{RequiredSamples} OK={_ok} Fail={_fail} " +
                       $"Rate={rate:0.00}% Avg={avg:0.0}ms Max={_maxLatencyMs}ms";
            }

            bool passRate = rate >= RequiredSuccessRate;
            bool passLatency = _maxLatencyMs <= RequiredMaxLatencyMs;
            string pass = (passRate && passLatency) ? "PASS" : "FAIL";

            return $"[{pass}] Total={_sent} OK={_ok} Fail={_fail} " +
                   $"Rate={rate:0.00}% Avg={avg:0.0}ms Max={_maxLatencyMs}ms";
        }


        private void UpdateKpi()
        {
            lblKpi.Text = "KPI: " + BuildKpiText();
        }

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            if (lstLog.InvokeRequired)
                lstLog.BeginInvoke((Action)(() => lstLog.Items.Add(line)));
            else
                lstLog.Items.Add(line);

            if (lstLog.InvokeRequired)
                lstLog.BeginInvoke((Action)(() => lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1)));
            else
                lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1);
        }

        private void ShowSummaryPopup()
        {
            // UI thread safe
            if (this.InvokeRequired)
            {
                this.BeginInvoke((Action)(ShowSummaryPopup));
                return;
            }

            string summary = BuildSummaryText();
            bool isPass = IsFinalPass();

            MessageBox.Show(
                summary,
                "Test",
                MessageBoxButtons.OK,
                isPass ? MessageBoxIcon.Information : MessageBoxIcon.Error
            );
        }

        private bool IsFinalPass()
        {
            double rate = _sent > 0 ? (double)_ok / _sent * 100.0 : 0;

            bool passSamples = _sent >= RequiredSamples;
            bool passRate = rate >= RequiredSuccessRate;
            bool passLatency = _maxLatencyMs <= RequiredMaxLatencyMs;

            return passSamples && passRate && passLatency;
        }

        private string BuildSummaryText()
        {
            double rate = _sent > 0 ? (double)_ok / _sent * 100.0 : 0;
            double avg = _ok > 0 ? (double)_okLatencySumMs / _ok : 0;

            bool pass = IsFinalPass();

            var sb = new StringBuilder();
            sb.AppendLine(ScenarioTitle);
            sb.AppendLine(new string('-', 56));
            sb.AppendLine($"Test Count      : {_sent} / {RequiredSamples}");
            sb.AppendLine($"Success / Fail  : {_ok} / {_fail}");
            sb.AppendLine($"Success Rate    : {rate:0.00}% ");
            sb.AppendLine($"Avg Response Time     : {avg:0.0} ms");
            sb.AppendLine($"Max Response Time     : {_maxLatencyMs} ms");
            sb.AppendLine(new string('-', 56));
            return sb.ToString();
        }

        private async Task<(bool ok, long ms, int httpCode)> PostOrderTimedAsync(
            string hubIp, string orderId, int timeoutMs, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var handler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    AutomaticDecompression = System.Net.DecompressionMethods.None,

                    // 禁止連線池（維持壓力測試一致性）
                    PooledConnectionLifetime = TimeSpan.Zero,
                    PooledConnectionIdleTimeout = TimeSpan.Zero,
                    MaxConnectionsPerServer = 1
                };

                using var client = new HttpClient(handler);
                client.Timeout = Timeout.InfiniteTimeSpan;

                using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                reqCts.CancelAfter(timeoutMs);

                string url = $"http://{hubIp}:8080/orders";
                string json = new JObject { ["orderId"] = orderId }.ToString();

                var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.ConnectionClose = true;
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, reqCts.Token);
                sw.Stop();

                int code = (int)resp.StatusCode;
                bool ok = code >= 200 && code < 300;
                return (ok, sw.ElapsedMilliseconds, code);
            }
            catch
            {
                sw.Stop();
                return (false, sw.ElapsedMilliseconds, 0);
            }
        }
    }
}
