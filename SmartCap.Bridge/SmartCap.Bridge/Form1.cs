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

        // 統計
        private int _sent = 0, _ok = 0, _fail = 0;
        private long _okLatencySumMs = 0, _maxLatencyMs = 0;
        private readonly List<long> _latencies = new List<long>();

        public Form1()
        {
            InitializeComponent();

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

            ResetStats();

            _running = true;
            btnRun.Enabled = false;
            btnStop.Enabled = true;
            btnExportCsv.Enabled = false;

            _cts = new CancellationTokenSource();

            Log($"🧪 START | hub={hubIp} orderId={orderId} count={count} interval={intervalMs}ms timeout={timeoutMs}ms");
           // Log("✅ 成功定義：HTTP 2xx");
            Log("⏱️ 反應時間：request→response (Stopwatch)");

            try
            {
                const int WorstCaseMs = 400;

                for (int i = 1; i <= count; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    _sent++;

                    var (ok, ms, httpCode) =
                     await PostOrderTimedAsync(hubIp, orderId, timeoutMs, _cts.Token);

                    // 加 300ms DELAY
                    int extraDelay = (i >= 0) ? 350 : 0;

                    // 🔴 調整後的 latency（唯一版本）
                    long latency = ms + extraDelay;

                    // 行為上真的補 delay
                    if (extraDelay > 0)
                    {
                        await Task.Delay(extraDelay, _cts.Token);
                    }

                    if (ok)
                    {
                        _ok++;
                        _okLatencySumMs += latency;     // ✅ AVG 用這個
                        _latencies.Add(latency);        // ✅ CSV 用這個
                        if (latency > _maxLatencyMs)    // ✅ MAX 用這個
                            _maxLatencyMs = latency;
                    }
                    else
                    {
                        _fail++;
                    }

                    UpdateKpi();

                    // ✅ LOG 只顯示一個 latency（已調整）
                    Log($"[TEST] i={i}/{count} http={(httpCode == 0 ? "ERR" : httpCode.ToString())} " +
                        $"ok={(ok ? "Y" : "N")} latency={latency}ms");

                    // 原本 interval（如需保留）
                    if (intervalMs > 0)
                        await Task.Delay(intervalMs, _cts.Token);

                }

                Log("✅ DONE | " + BuildKpiText());
                btnExportCsv.Enabled = _latencies.Count > 0;
            }
            catch (OperationCanceledException)
            {
                Log("⏹️ 已停止（Stop）");
                btnExportCsv.Enabled = _latencies.Count > 0;
            }
            catch (Exception ex)
            {
                Log("❌ 例外：" + ex.Message);
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
                    FileName = $"c3_stress_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                // CSV: index, latency_ms
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
            //string pass = (_maxLatencyMs <= 500) ? "PASS" : "FAIL";
            return $"Total={_sent} OK={_ok} Fail={_fail} Rate={rate:0.00}% Avg={avg:0.0}ms Max={_maxLatencyMs}ms ";
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

            // 自動捲到底
            if (lstLog.InvokeRequired)
                lstLog.BeginInvoke((Action)(() => lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1)));
            else
                lstLog.TopIndex = Math.Max(0, lstLog.Items.Count - 1);
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

                    // 🔴 關鍵：禁止連線池
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
                req.Headers.ConnectionClose = true;   // 🔴 再補一層
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
