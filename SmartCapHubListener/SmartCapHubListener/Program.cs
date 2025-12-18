using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

// 先把程式包在命名空間裡（避免「最上層陳述式」錯誤）
namespace SmartCapHubListener
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "SmartCAP Hub Listener";

            // 讀設定
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var hubIp = config["HubIp"] ?? "10.0.60.97";
            var centralUrl = config["CentralUrl"] ?? "http://localhost:8080";
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            // 讀按鈕→料框對應
            Dictionary<string, string> btnMap = new();
            try
            {
                var text = await File.ReadAllTextAsync("mapping.buttons.json");
                btnMap = JsonSerializer.Deserialize<Dictionary<string, string>>(text)
                         ?? new Dictionary<string, string>();
                Log($"Loaded button mapping: {btnMap.Count} items.");
            }
            catch (Exception ex)
            {
                Warn($"mapping.buttons.json not found or invalid: {ex.Message}");
            }

            // 啟動 HUB（暫時用 stub）
            var hub = new SmartCapHubStub(hubIp);
            hub.ButtonPressed += async (_, e) =>
            {
                var buttonId = e.ButtonId;
                Log($"Button pressed: {buttonId}");

                if (btnMap.TryGetValue(buttonId, out var binId))
                {
                    var payload = new { binId, pressed = true };
                    try
                    {
                        var resp = await http.PostAsJsonAsync($"{centralUrl}/button", payload);
                        Log($"POST {centralUrl}/button => {(int)resp.StatusCode}");
                    }
                    catch (Exception ex)
                    {
                        Error($"POST /button failed: {ex.Message}");
                    }
                }
                else
                {
                    Warn($"No bin mapping for button {buttonId}");
                }
            };

            Log($"Connecting HUB {hubIp} ...");
            await hub.ConnectAsync();

            // AutoLight (選擇性)
            try
            {
                var autoLightSection = config.GetSection("AutoLight");
                var autoLights = autoLightSection.Get<List<AutoLightItem>>() ?? new();
                foreach (var item in autoLights)
                {
                    await hub.SetButtonColorAsync(item.ButtonId, item.Color ?? "COLGREEN");
                    Log($"Auto light {item.ButtonId} -> {item.Color}");
                }
            }
            catch (Exception ex)
            {
                Warn($"AutoLight section error: {ex.Message}");
            }

            Log("Running. Press Ctrl+C to exit.");
            await WaitForCtrlCAsync();
        }

        // ===== 其他方法 =====
        record AutoLightItem(string ButtonId, string? Color);

        class SmartCapHubStub
        {
            public event EventHandler<ButtonEventArgs>? ButtonPressed;
            private readonly string _ip;

            public SmartCapHubStub(string ip) => _ip = ip;

            public Task ConnectAsync()
            {
                Log($"[STUB] Connected to HUB {_ip}");
                _ = SimulateButtonLoop();
                return Task.CompletedTask;
            }

            public Task SetButtonColorAsync(string buttonId, string color)
            {
                Log($"[STUB] SetButtonColor {buttonId} -> {color}");
                return Task.CompletedTask;
            }

            private async Task SimulateButtonLoop()
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15));
                    ButtonPressed?.Invoke(this, new ButtonEventArgs("B-01-01"));
                }
            }
        }

        class ButtonEventArgs : EventArgs
        {
            public string ButtonId { get; }
            public ButtonEventArgs(string buttonId) => ButtonId = buttonId;
        }

        static Task WaitForCtrlCAsync()
        {
            var tcs = new TaskCompletionSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; tcs.TrySetResult(); };
            return tcs.Task;
        }

        static void Log(string msg) => Console.WriteLine($"[INFO] {msg}");
        static void Warn(string msg) => Console.WriteLine($"[WARN] {msg}");
        static void Error(string msg) => Console.WriteLine($"[ERR ] {msg}");
    }
}
