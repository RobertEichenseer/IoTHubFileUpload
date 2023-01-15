# SDK Example uploading files to IoT Hub

## Azure Environment Creation
[Azure CLI file](/src/EnvCreation/EnvCreationAndDemo.azcli) can be used to create the necessary demo environment: 

- Step 1: Logon to Azure and select the default subscription
- Step 2: Create a project unifier to ensure that IoT Hub and storage account can be created
- Step 3: Create Azure Resource Group 
- Step 4: Create IoT Hub Instance. A S1 SKU of IoT Hub with two partitions will be created. 
- Step 5: Create a storage account and a container where files will be uploaded to. 
- Step 6: Update IoT Hub
    - IoT Hub will be "linked" to crated storage account. This allows IoT Hub to create SAS URIs which are used by a device to http post a file to the storage container.

    - Parameter: 
        ```
        //Defines live time of a SaaS created by IoT Hub on request of a device to ne hour (default)
            ...
            $uploadSasTtl = 1  
            ...
            az iot hub update `
                --name $hubName `
                --fileupload-storage-auth-type $storageAuthType `
                --fileupload-storage-connectionstring $storageConnectionString `
                --fileupload-storage-container-name $storageContainerName `
                --fileupload-notifications $uploadNotifications `
                --fileupload-notification-max-delivery-count $uploadNotificationMaxDelivery `
                --fileupload-notification-ttl $uploadNotificationTtl `
                --fileupload-sas-ttl $uploadSasTtl  
        ```
- Step 7: Creates a device, retrieves the DeviceConnection String and starts the c# demo application. 

## C# demo application
### "ClassicFileUpload"
Showcases the deprecated SDK method *UploadToBlobAsync()*. *UploadToBlobAsync()* is easy to use, hides all implementation details but doesn't provide fine granular access to e.g. CorrelationIds. The method is deprecated and should not be used in new projects. 

```
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
```

### "RecommendedFileUpload"
Showcases the recommended approach of: 
- Requesting a SAS Uri; *GetFileUploadSasUriAsync()*
- Uploading a file using the Azure Storage SDK; *blockBlobClient.UploadAsync()*
- Updating IoT Hub regarding the upload status; *CompleteFileUploadAsync()* 

### "DeviceQuotaExcess"
Showcases:
- Request of 10 (IoT Hub concurrent file upload device quota) SAS Uris
- Request an additional SAS Uri. Request will fail!
- Release requested SAS Uris
- Request an additional SAS Uri. Request succeeds.

### "RequestAndRelease"
Showcases:
- Request of 10 (IoT Hub concurrent file upload device quota) SAS Uris
- Persisting of the SAS Uris in a file
- Simulating a device error by Disposing the used DeviceClient object
- Creation of a new DeviceClient (to simulate a device re-start), reading the persisted CorrelationIds and releasing the SAS URIs based on the CorrelationIds