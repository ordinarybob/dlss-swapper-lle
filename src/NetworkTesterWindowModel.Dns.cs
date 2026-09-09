using System;
using System.Net;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace DLSS_Swapper;

public partial class NetworkTesterWindowModel
{
    [RelayCommand]
    async Task RunTest9Async()
    {
        var cancellationTokenSource = StartTest();
        var cancellationToken = cancellationTokenSource.Token;
        var testName = "Test 9";
        RunningTest9 = true;
        Test9Result = string.Empty;
        var testStart = DateTime.Now;
        AppendTestResults(testName, $"DNS lookup of DLSS Swapper file server ({_dlssSwapperDomainTestLink})");

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(_dlssSwapperDomainTestLink, cancellationToken);

            foreach (var address in addresses)
            {
                AppendTestResults(testName, $"Found IP address {address.ToString()}");
            }

            Test9Result = "✅";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Test9Result = string.Empty;
            AppendTestResults(testName, "Cancelled");
        }
        catch (Exception err)
        {
            Test9Result = "❌";
            AppendTestResults(testName, $"Failed, {err.Message}");
            if (string.IsNullOrWhiteSpace(err.InnerException?.Message) == false)
            {
                AppendTestResults(testName, $"Inner Exception: {err.InnerException.Message}");
            }
            RunningTest9 = false;
        }
        finally
        {
            CompleteTest(cancellationTokenSource);
            var duration = (DateTime.Now - testStart).TotalSeconds;
            AppendTestResults(testName, $"Duration {duration:0.00} seconds\n");
            RunningTest9 = false;
        }
    }
}
