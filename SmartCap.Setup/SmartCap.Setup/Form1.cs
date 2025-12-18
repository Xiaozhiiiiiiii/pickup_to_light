// Form1.cs
using smartCAP_SDK;
using smartCAP_SDK.Messages;
using smartCAP_SDK.Common.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SmartCap.Setup
{
    public partial class Form1 : Form
    {
        // SDK 服務實例
        private SmartCAPService? _svc;
        private readonly string defaultAddress = "ALL_SENSORS";

        // 掃描到的 Hub 物件清單（與下拉選單一一對應）
        private readonly List<object> _foundHubs = new List<object>();

        public Form1()
        {
            InitializeComponent();
            TryBindSdk();
            InitializeAppearanceUi(); // 讓 PRE/POST 下拉 & 預設值 ready
        }

        // 建立服務實例
        private void TryBindSdk()
        {
            try
            {
                _svc = new SmartCAPService();
                Log("✅ 已建立 SmartCAPService 實例 (3.0.3.x / 3.1 相容)");
                Log("👉 請先按「掃描 HUB」或手動輸入 IP，然後按「連線」");
            }
            catch (Exception ex)
            {
                Log("❌ 綁定 SDK 失敗：" + ex.Message);
            }
        }

        // ========= 掃描 HUB（用 SensorhubFinder） =========
        private void btnScan_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_svc == null)
                {
                    Log("⚠️ 尚未初始化 SDK");
                    return;
                }

                cmbHubList.Items.Clear();
                _foundHubs.Clear();

                var hubs = SensorhubFinder.Find();
                if (hubs != null && hubs.Count > 0)
                {
                    int idx = 0;
                    foreach (var hub in hubs)
                    {
                        _foundHubs.Add(hub);
                        cmbHubList.Items.Add(GetHubLabel(hub, ++idx));
                    }
                    Log($"✅ 找到 {hubs.Count} 台 HUB（SensorhubFinder）");
                    if (cmbHubList.Items.Count > 0) cmbHubList.SelectedIndex = 0;
                }
                else
                {
                    Log("❌ 沒掃到 HUB（SensorhubFinder.Find() 回傳空）");
                }
            }
            catch (Exception ex)
            {
                Log("❌ 掃描失敗：" + ex.Message);
            }
        }

        // ========= 連線（優先用 hub 物件；否則用手動 IP） =========
        private void btnConnect_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_svc == null)
                {
                    Log("⚠️ 尚未初始化 SDK");
                    return;
                }

                // 優先使用掃描結果
                if (cmbHubList.SelectedIndex >= 0 && cmbHubList.SelectedIndex < _foundHubs.Count)
                {
                    var hub = _foundHubs[cmbHubList.SelectedIndex];
                    bool ok = InvokeInitWithHub(_svc, hub);
                    Log($"🔗 Init(hub) -> {(ok ? "成功" : "失敗")}");
                    if (ok)
                    {
                        SendPre();
                        SendBanner(null, null); // 用目前 PRE 面板值送 Banner
                    }
                    return;
                }

                // 回退：手動輸入 IP
                var ip = (cmbHubList.SelectedItem ?? cmbHubList.Text)?.ToString();
                if (string.IsNullOrWhiteSpace(ip))
                {
                    MessageBox.Show("請先掃描或輸入 HUB IP");
                    return;
                }

                bool ok2 = _svc.Init(ip, 4040, false);
                Log($"🔗 Init({ip},4040,false) -> {(ok2 ? "成功" : "失敗")}");
                if (ok2)
                {
                    SendPre();
                    SendBanner(null, null);
                }
            }
            catch (Exception ex)
            {
                Log("❌ 連線失敗：" + ex.Message);
            }
        }

        // ========= 綁定位址 =========
        // ========= 綁定位址 =========
        private void btnAssign_Click(object? sender, EventArgs e)
        {
            if (_svc == null)
            {
                Log("⚠️ 尚未初始化 SDK");
                return;
            }

            var addrText = txtAddress.Text.Trim();
            if (!int.TryParse(addrText, out var addr) || addr < 1 || addr > 65534)
            {
                MessageBox.Show("請輸入正確的位址（1~65534），建議從 1000 開始");
                return;
            }

            try
            {
                // 1) （可選）先顯示一個 PRE 效果，提示「進入綁定模式」
                _svc.Send(new MessageToSensor
                {
                    Address = "ALL_SENSORS",
                    CommandType = "SET_PARAMETER",
                    Offset = "PRE_BUTTON_MODE",
                    // 想低調一點就把顏色換成白色、或改成你面板選的顏色/文字
                    Payload = "ENABLED//COLCYAN/SOLID_RING/@Addr@"
                });

                // 2) 全部切到 PRE（文件建議）
                _svc.Send(new MessageToSensor
                {
                    Address = "ALL_SENSORS",
                    CommandType = "SET_STATUS",
                    Offset = "SPECIAL_COMMANDS",
                    Payload = "PRE_PRESSED_STATE"
                });

                // 3) 指派位址（關鍵差異：Offset=ADDRESS_TO_BE_ASSIGNED；Payload 僅數字）
                _svc.Send(new MessageToSensor
                {
                    Address = "ALL_SENSORS",
                    CommandType = "SET_STATUS",
                    Offset = "ADDRESS_TO_BE_ASSIGNED",
                    Payload = addr.ToString()
                });

                Log($"📮 已送出位址指派：下一顆被觸碰的按鈕將被綁為 {addr}（請觸碰該按鈕）");
            }
            catch (Exception ex)
            {
                Log("❌ 綁定位址指令送出失敗：" + ex.Message);
            }
        }


        // ========= PRE / POST 外觀 =========
        private void btnPre_Click(object? sender, EventArgs e)
        {
            var color = (cmbPreColor?.SelectedItem?.ToString() ?? "COLCYAN").Trim();
            var effect = (cmbPreEffect?.SelectedItem?.ToString() ?? "SOLID_RING").Trim();
            var text = WrapAtIfNeeded(txtPreText?.Text ?? "@Go@");

            var payload = BuildPrePayload(color, effect, text);
            SendToHub("ALL_SENSORS", "SET_PARAMETER", "PRE_BUTTON_MODE", payload);
            Log($"💡 已設定 PRE 外觀：{payload}");
        }

        private void btnPost_Click(object? sender, EventArgs e)
        {
            var color = (cmbPostColor?.SelectedItem?.ToString() ?? "COLGREEN").Trim();
            var effect = (cmbPostEffect?.SelectedItem?.ToString() ?? "SOLID_RING").Trim();
            var text = WrapAtIfNeeded(txtPostText?.Text ?? "donE");

            var payload = BuildPostPayload(color, effect, text);
            SendToHub("ALL_SENSORS", "SET_PARAMETER", "POST_BUTTON_MODE", payload);
            Log($"💡 已設定 POST 外觀：{payload}");
        }

        // ========= 設定輪詢（示範） =========
        private void btnPolling_Click(object? sender, EventArgs e)
        {
            var list = Microsoft.VisualBasic.Interaction.InputBox(
                "輸入要啟用輪詢的位址（用 / 分隔，例如 1001/1002）",
                "設定輪詢",
                "1001/1002").Trim();

            if (string.IsNullOrWhiteSpace(list)) return;

            // TODO: 依官方文件確認 Offset 名稱
            SendToHub("HUB", "SET_PARAMETER", "POLLING_LIST", list);
            Log($"🔁 已設定輪詢名單：{list}");
        }

        // ====== 發送封裝 ======
        private void SendPre()
        {
            if (_svc == null) return;

            _svc.Send(new MessageToSensor
            {
                Address = defaultAddress,
                CommandType = "SET_STATUS",
                Offset = "SPECIAL_COMMANDS",
                Payload = "PRE_PRESSED_STATE"
            });
        }

        // 若 text/color 傳 null，則讀用當前 PRE 面板的設定
        private void SendBanner(string? text, string? color)
        {
            if (_svc == null) return;

            var c = (cmbPreColor?.SelectedItem?.ToString() ?? color ?? "COLWHITE").Trim();
            var ef = (cmbPreEffect?.SelectedItem?.ToString() ?? "SOLID_RING").Trim();
            var tx = text;
            if (string.IsNullOrWhiteSpace(tx)) tx = txtPreText?.Text ?? "@Go@";
            tx = WrapAtIfNeeded(tx);

            _svc.Send(new MessageToSensor
            {
                Address = defaultAddress,
                CommandType = "SET_PARAMETER",
                Offset = "PRE_BUTTON_MODE",
                Payload = $"ENABLED//{c}/{ef}/{tx}"
            });
        }

        private void SendToHub(string address, string commandType, string offset, string payload)
        {
            try
            {
                if (_svc == null)
                {
                    Log("⚠️ 尚未初始化 SDK");
                    return;
                }

                _svc.Send(new MessageToSensor
                {
                    Address = address,
                    CommandType = commandType,
                    Offset = offset,
                    Payload = payload
                });

                Log($"[SDK→Send] {address} | {commandType} | {offset} | {payload}");
            }
            catch (Exception ex)
            {
                Log("❌ 發送失敗：" + ex.Message);
            }
        }

        // ====== 外觀 UI 初始化（填選項 + 預設值） ======
        private void InitializeAppearanceUi()
        {
            var colors = new[] { "COLWHITE", "COLGREEN", "COLRED", "COLBLUE", "COLCYAN", "COLMAGENTA", "COLYELLOW", "COLORANGE" };
            var effects = new[] { "SOLID_RING", "BREATHING_RING", "RUNNING_RING" }; // 視韌體支援調整

            cmbPreColor.Items.Clear(); cmbPreColor.Items.AddRange(colors);
            cmbPostColor.Items.Clear(); cmbPostColor.Items.AddRange(colors);
            cmbPreEffect.Items.Clear(); cmbPreEffect.Items.AddRange(effects);
            cmbPostEffect.Items.Clear(); cmbPostEffect.Items.AddRange(effects);

            cmbPreColor.SelectedItem = "COLCYAN";
            cmbPostColor.SelectedItem = "COLGREEN";
            cmbPreEffect.SelectedItem = "SOLID_RING";
            cmbPostEffect.SelectedItem = "SOLID_RING";

            if (string.IsNullOrWhiteSpace(txtPreText.Text)) txtPreText.Text = "@Go@";
            if (string.IsNullOrWhiteSpace(txtPostText.Text)) txtPostText.Text = "donE";
        }

        // ====== 工具：包 @ 與 Payload Builder ======
        private static string WrapAtIfNeeded(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "@@";
            var t = s.Trim();
            if (!t.StartsWith("@")) t = "@" + t;
            if (!t.EndsWith("@")) t = t + "@";
            return t;
        }

        private static string BuildPrePayload(string color, string effect, string text)
            => $"ENABLED//{color}/{effect}/{text}";

        private static string BuildPostPayload(string color, string effect, string text)
            => $"DISABLED//{color}/{effect}/{text}";

        // ====== Log ======
        private void Log(string msg)
        {
            try
            {
                if (this.lstLog.InvokeRequired)
                    this.lstLog.Invoke((Action)(() => this.lstLog.Items.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}")));
                else
                    this.lstLog.Items.Add($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
            }
            catch { /* ignore when closing */ }
        }

        // ====== 掃描清單顯示 & Init 相容處理 ======
        private string GetHubLabel(object hub, int index)
        {
            try
            {
                var t = hub.GetType();
                var ipProp = t.GetProperty("Ip") ?? t.GetProperty("IP") ?? t.GetProperty("IpAddress") ?? t.GetProperty("Address");
                var nameProp = t.GetProperty("Name") ?? t.GetProperty("Hostname");
                var ip = ipProp?.GetValue(hub)?.ToString();
                var name = nameProp?.GetValue(hub)?.ToString();

                if (!string.IsNullOrWhiteSpace(ip) && !string.IsNullOrWhiteSpace(name))
                    return $"[{index}] {name} ({ip})";
                if (!string.IsNullOrWhiteSpace(ip))
                    return $"[{index}] {ip}";
                if (!string.IsNullOrWhiteSpace(name))
                    return $"[{index}] {name}";
                return $"[{index}] {hub}";
            }
            catch
            {
                return $"[{index}] {hub}";
            }
        }

        private bool InvokeInitWithHub(SmartCAPService svc, object hub)
        {
            try
            {
                var tSvc = svc.GetType();
                var methods = tSvc.GetMethods().Where(m => m.Name == "Init").ToList();

                // 先找單一參數 Init(hub型別)
                var init1 = methods.FirstOrDefault(m => m.GetParameters().Length == 1);
                if (init1 != null)
                {
                    var ret = init1.Invoke(svc, new object[] { hub });
                    return (ret is bool b) ? b : true;
                }

                // 退回：抽 IP -> Init(string,int,bool) / Init(string)
                string ip = ExtractIpFromHub(hub);
                if (string.IsNullOrWhiteSpace(ip))
                    throw new InvalidOperationException("無法從 hub 物件取得 IP");

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
                    var ret = init3.Invoke(svc, new object[] { ip, 4040, false });
                    return (ret is bool b3) ? b3 : true;
                }

                var initStr = methods.FirstOrDefault(m =>
                {
                    var ps = m.GetParameters();
                    return ps.Length == 1 && ps[0].ParameterType == typeof(string);
                });
                if (initStr != null)
                {
                    var ret = initStr.Invoke(svc, new object[] { ip });
                    return (ret is bool b2) ? b2 : true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log("❌ InvokeInitWithHub 失敗：" + ex.Message);
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
                var m = System.Text.RegularExpressions.Regex.Match(s, @"\b\d{1,3}(\.\d{1,3}){3}\b");
                return m.Success ? m.Value : "";
            }
            catch { return ""; }
        }
    }
}
