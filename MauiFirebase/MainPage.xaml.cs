
using Microsoft.Maui.Controls;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MauiFirebase;

public partial class MainPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private const string RealtimeDbUrl = "https://maui-firebase-ea092-default-rtdb.firebaseio.com/Seasoning";
    private const string FirestoreUrl = "https://firestore.googleapis.com/v1/projects/maui-firebase-ea092/databases/(default)/documents/SeasoningSync/latestStatus";
    private const string FirestoreLogUrl = "https://firestore.googleapis.com/v1/projects/maui-firebase-ea092/databases/(default)/documents/DispenseLogs?documentId=";

    public MainPage()
    {
        InitializeComponent();
        StartRealtimeListener(); // ✅ Correct method name
    }

    private async void StartRealtimeListener()
    {
        while (true)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{RealtimeDbUrl}.json");
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(response);

                if (data != null)
                {
                    ChiliStatus.Text = $"Chili: {data["Chili"]}";
                    PepperStatus.Text = $"Pepper: {data["Pepper"]}";
                    SaltStatus.Text = $"Salt: {data["Salt"]}";

                    // ✅ Sync to Firestore
                    var firestorePayload = new
                    {
                        fields = new
                        {
                            Chili = new { stringValue = data["Chili"] },
                            Pepper = new { stringValue = data["Pepper"] },
                            Salt = new { stringValue = data["Salt"] },
                            syncedAt = new { stringValue = DateTime.UtcNow.ToString("o") }
                        }
                    };

                    var json = JsonSerializer.Serialize(firestorePayload);
                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), FirestoreUrl)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                    await _httpClient.SendAsync(request);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Realtime listener error: {ex.Message}");
            }

            await Task.Delay(3000); // Poll every 3 seconds
        }
    }

    private async void OnDropClicked(object sender, EventArgs e)
    {
        var button = (Button)sender;
        var type = button.CommandParameter?.ToString();

        if (string.IsNullOrEmpty(type)) return;

        try
        {
            // ✅ Update Realtime DB
            await _httpClient.PutAsync($"{RealtimeDbUrl}/{type}.json", new StringContent("\"Dropping…\"", Encoding.UTF8, "application/json"));
            await Task.Delay(2000);
            await _httpClient.PutAsync($"{RealtimeDbUrl}/{type}.json", new StringContent("\"Stopped\"", Encoding.UTF8, "application/json"));

            // ✅ Log to Firestore
            var logPayload = new
            {
                fields = new
                {
                    seasoning = new { stringValue = type },
                    action = new { stringValue = "Dropped" },
                    timestamp = new { stringValue = DateTime.UtcNow.ToString("o") }
                }
            };

            var logJson = JsonSerializer.Serialize(logPayload);
            var logId = Guid.NewGuid().ToString();
            await _httpClient.PostAsync(FirestoreLogUrl + logId, new StringContent(logJson, Encoding.UTF8, "application/json"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Drop error: {ex.Message}");
        }
    }
}
