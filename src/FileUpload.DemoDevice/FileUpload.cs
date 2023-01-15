using System.Collections.Generic;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;

internal class FileUpload 
{
    internal DeviceClient _deviceClient; 

    internal readonly int _iotHubDeviceQuota = 10; 

    public FileUpload(DeviceClient deviceClient)
    {
        _deviceClient = deviceClient; 
    }

    
    internal async Task<bool> ClassicFileUpload(string fileName)
    {
        using var fileStreamSource = new FileStream(fileName, FileMode.Open);
        fileName = Path.GetFileName(fileStreamSource.Name); 
        try {
            await _deviceClient.UploadToBlobAsync(fileName.ToLower(), fileStreamSource);
        } catch (Exception)
        {
            return false; 
        }
        return true; 
    }

    internal async Task<bool> RecommendedFileUpload(string fileName)
    {

    FileUploadCompletionNotification fileUploadCompletionNotification; 

        using var fileStreamSource = new FileStream(fileName, FileMode.Open);
        fileName = Path.GetFileName(fileStreamSource.Name); 

        var fileUploadSasUriRequest = new FileUploadSasUriRequest {
                BlobName = fileName
        };

        FileUploadSasUriResponse sasUri = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest); 
        Uri uploadUri = sasUri.GetBlobUri(); 
        try {
            BlockBlobClient blockBlobClient = new BlockBlobClient(uploadUri);
            await blockBlobClient.UploadAsync(fileStreamSource, new BlobUploadOptions());
        } 
        catch (Exception ex) 
        {
            fileUploadCompletionNotification = new FileUploadCompletionNotification(){
                CorrelationId = sasUri.CorrelationId,
                IsSuccess = false
            };
            await _deviceClient.CompleteFileUploadAsync(fileUploadCompletionNotification);
            return false; 
        }

        fileUploadCompletionNotification = new FileUploadCompletionNotification(){
            CorrelationId = sasUri.CorrelationId, 
            IsSuccess = true
        };
        await _deviceClient.CompleteFileUploadAsync(fileUploadCompletionNotification);

        return true; 
    }

    internal async Task<bool> DeviceQuotaExcess(string fileName)
    {
        List<string> correlationIds = new List<string>(); 
        
        FileUploadSasUriRequest fileUploadSasUriRequest; 
        FileUploadSasUriResponse fileUploadSasUriResponse;
        FileUploadCompletionNotification fileUploadCompletionNotification; 

        //Request the maximum amount of SAS (10) per device to upload files in parallel 
        for (int i=0; i<_iotHubDeviceQuota; i++){
            try {
                using var fileStreamSource = new FileStream(fileName, FileMode.Open);
                fileName = Path.GetFileName(fileStreamSource.Name); 

                fileUploadSasUriRequest = new FileUploadSasUriRequest {
                    BlobName = fileName
                };
                var sasUri = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest); 
                correlationIds.Add(sasUri.CorrelationId); 
            }
            catch (Exception) {
                return false; 
            }
        }

        //Request an additional SAS - Request will fail. 
        try {
            fileUploadSasUriRequest = new FileUploadSasUriRequest {
                BlobName = fileName
            };
            fileUploadSasUriResponse = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest); 
        } 
        catch (Exception ex) {
            //Exception is expected
        }

        //Release SAS with IoT Hub 
        foreach(string correlationId in correlationIds)
        {
            fileUploadCompletionNotification = new FileUploadCompletionNotification(){
                CorrelationId = correlationId, 
                IsSuccess = true
            };
            try {
                await _deviceClient.CompleteFileUploadAsync(fileUploadCompletionNotification); 
            } catch (Exception) {
                return false; 
            }
        }

        //Request an additional SAS - Request will succeed. 
        try {
            fileUploadSasUriRequest = new FileUploadSasUriRequest {
                BlobName = fileName
            };
            fileUploadSasUriResponse = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest); 

            //File upload code

            await _deviceClient.CompleteFileUploadAsync(new FileUploadCompletionNotification(){
                CorrelationId = fileUploadSasUriResponse.CorrelationId,
                IsSuccess = true,
            }); 
        } 
        catch (Exception ex) {
            return false; 
        }

        return true; 
    }

    internal async Task<List<string>> RequestSAS(string fileName)
    {
        List<string> correlationIds = new List<string>(); 

        FileUploadSasUriRequest fileUploadSasUriRequest; 
        
        //Request the maximum amount of SAS (10) per device to upload files in parallel 
        for (int i=0; i<_iotHubDeviceQuota; i++){
            try {
                using var fileStreamSource = new FileStream(fileName, FileMode.Open);
                fileName = Path.GetFileName(fileStreamSource.Name); 

                fileUploadSasUriRequest = new FileUploadSasUriRequest {
                    BlobName = fileName
                };
                var sasUri = await _deviceClient.GetFileUploadSasUriAsync(fileUploadSasUriRequest); 
                correlationIds.Add(sasUri.CorrelationId); 
            }
            catch (Exception) {
                return correlationIds; 
            }
        }
        return correlationIds; 
   }

    internal async Task<bool> ReleaseSAS(List<string> correlationIds)
    {
        FileUploadCompletionNotification fileUploadCompletionNotification; 

        foreach(string correlationId in correlationIds)
        {
            fileUploadCompletionNotification = new FileUploadCompletionNotification(){
                CorrelationId = correlationId, 
                IsSuccess = true
            };
            try {
                await _deviceClient.CompleteFileUploadAsync(fileUploadCompletionNotification); 
            } catch (Exception) {
                return false; 
            }
        }       
        return true; 
    }
}