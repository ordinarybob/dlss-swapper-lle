using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper;
using DlssSwapper.Shared;

internal static class DnsAndIconTests
{
    public static async Task RunAsync()
    {
        void Check(bool value)
        {
            if (!value) throw new Exception("DNS/icon contract failed.");
        }
        var model = new NetworkTesterWindowModel { CancelBeforeStart = true };
        await model.RunTest9Command.ExecuteAsync(null);
        Check(!model.RunningTest9 && model.Test9Result == "" && model.Completed == 1 && model.Messages.Contains("Cancelled"));
        model = new NetworkTesterWindowModel();
        await model.RunTest9Command.ExecuteAsync(null);
        Check(!model.RunningTest9 && model.Test9Result == "✅" && model.Completed == 1);
        model = new NetworkTesterWindowModel { Domain = new string('a', 256) };
        await model.RunTest9Command.ExecuteAsync(null);
        Check(!model.RunningTest9 && model.Test9Result == "❌" && model.Completed == 1);
        var icon = WindowsIconReference.Parse("\"C:\\Games\\Title, Edition\\game.exe\", -123");
        Check(icon.Path == "C:\\Games\\Title, Edition\\game.exe" && icon.Index == -123);
        Check(WindowsIconReference.Parse("C:\\Games\\game.dll,2").Index == 2);
        Check(WindowsIconReference.Parse("C:\\Games\\game.exe").Index == 0);
        Check(WindowsIconReference.Parse("C:\\Games\\Title, Edition\\game.exe").Path.EndsWith("game.exe"));
        foreach (var invalid in new[] { "", "\"unterminated", "\"game.exe\",not-an-index", "bad\npath" })
        {
            try { WindowsIconReference.Parse(invalid); throw new Exception("Invalid icon reference accepted."); }
            catch (FormatException) { }
        }
        Console.WriteLine("DNS command: cancelled/success/failure cleanup passed using loopback literal only. Icon parser: quoted paths, commas, signed indices and invalid references passed.");
    }
}

namespace DLSS_Swapper
{
    public partial class NetworkTesterWindowModel : ObservableObject
    {
        public string Domain { get; set; } = "127.0.0.1";
        string _dlssSwapperDomainTestLink => Domain;
        public bool CancelBeforeStart { get; set; }
        public bool RunningTest9 { get; set; }
        public string Test9Result { get; set; } = "";
        public int Completed { get; private set; }
        public List<string> Messages { get; } = [];
        CancellationTokenSource StartTest()
        {
            var source = new CancellationTokenSource();
            if (CancelBeforeStart) source.Cancel();
            return source;
        }
        void CompleteTest(CancellationTokenSource source) { Completed++; source.Dispose(); }
        void AppendTestResults(string name, string message) => Messages.Add(message);
    }
}
