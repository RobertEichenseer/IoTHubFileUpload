using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost consoleHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => {
        services.AddTransient<Main>();
    })
    .Build();

Main main = consoleHost.Services.GetRequiredService<Main>();
await main.ExecuteAsync(args);

class Main
{

    internal static string _fileToUpload = "DemoFile.txt";

    public async Task<int> ExecuteAsync(string[] args)
    {
        //Parse command line
        if (args.Count() != 2)
            return -1; 
        string deviceConnectionString = args[0]; 
        string action = args[1]; 

        //Create file to be uploaded
        File.WriteAllBytes(_fileToUpload, new byte[10000]); 

        //Create default DeviceClient
        using DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
        FileUpload fileUpload = new FileUpload(deviceClient); 

        //Execute requested action
        switch(action) {
            case "ClassicFileUpload": 
            {
                //Upload process is nicely hidden within the SDK
                Console.WriteLine("Classic File Upload");
                Console.WriteLine($"Succeeded: {await fileUpload.ClassicFileUpload(_fileToUpload)}");
                await deviceClient.CloseAsync();
                break; 
            }
            case "RecommendedFileUpload":
            {
                //Upload process with fine granular control
                Console.WriteLine("Recommended File Upload");
                Console.WriteLine($"Succeeded: {await fileUpload.RecommendedFileUpload(_fileToUpload)}");
                await deviceClient.CloseAsync();
                break; 
            }
            case "DeviceQuotaExcess":{
                //demo device quota
                Console.WriteLine("DeviceQuotaExcess");
                Console.WriteLine($"Succeeded: {await fileUpload.DeviceQuotaExcess(_fileToUpload)}");
                await deviceClient.CloseAsync();
                break; 
            }
            case "RequestAndRelease": {
                //Request 10 concurrent uploads and store correlation ids in a file
                Console.WriteLine("Request and Release");
                List<string> correlationIds = new List<string>();
                correlationIds = await fileUpload.RequestSAS(_fileToUpload); 
                Console.WriteLine($"Requested : {correlationIds.Count() == fileUpload._iotHubDeviceQuota}");
                //Persist provided CorrelationIds to file
                //This allows release of the CorrelationIds in case of a device problem
                await File.WriteAllLinesAsync("CorrelationIds.txt", correlationIds, System.Text.Encoding.UTF8);

                //Simulate a application restart
                await deviceClient.CloseAsync();

                //CorrelationIds are restored based on previously persisted file. A new DeviceClient is used to release the requested SAS Uris
                correlationIds = (await File.ReadAllLinesAsync("CorrelationIds.txt", System.Text.Encoding.UTF8)).ToList<string>();
                using DeviceClient deviceClientAfterRestart = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
                fileUpload._deviceClient = deviceClientAfterRestart; 
                
                //Release upload requests based on persisted correlation ids
                Console.WriteLine($"Release succeeded: {await fileUpload.ReleaseSAS(correlationIds)}");

                //await deviceClientAfterRestart.CloseAsync(); 
                break; 
            }
            default: {
                Console.WriteLine($"Unknown activity: {action}"); 
                break; 
            }
        }
        return 0;  
    }
}

