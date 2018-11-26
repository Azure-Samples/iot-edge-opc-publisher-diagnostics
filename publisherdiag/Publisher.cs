
using Newtonsoft.Json;
using System;
using System.Threading;

namespace PubisherDiag
{
    using Microsoft.Azure.Devices;
    using OpcPublisher;
    using System.Net;
    using System.Threading.Tasks;
    using static Program;

    public class Publisher
    {
        /// <summary>
        /// Ctor of Publisher class.
        /// </summary>
        public Publisher(string iotHubConnectionString, string iotHubPublisherDeviceName, string iotHubPublisherModuleName, CancellationToken ct)
        {
            // init IoTHub connection
            _iotHubClient = ServiceClient.CreateFromConnectionString(iotHubConnectionString, TransportType.Amqp_WebSocket_Only);
            _publisherDeviceName = iotHubPublisherDeviceName;
            _publisherDevice = new Device(iotHubPublisherDeviceName);
            _publisherModule = null;
            if (!string.IsNullOrEmpty(iotHubPublisherModuleName))
            {
                _publisherModuleName = iotHubPublisherModuleName;
                _publisherModule = new Module(iotHubPublisherDeviceName, iotHubPublisherModuleName);
            }
            TimeSpan responseTimeout = TimeSpan.FromSeconds(300);
            TimeSpan connectionTimeout = TimeSpan.FromSeconds(120);
            _getDiagnosticInfoMethod = new CloudToDeviceMethod("GetDiagnosticInfo", responseTimeout, connectionTimeout);
            _getDiagnosticLogMethod = new CloudToDeviceMethod("GetDiagnosticLog", responseTimeout, connectionTimeout);
            _exitApplicationMethod = new CloudToDeviceMethod("ExitApplication", responseTimeout, connectionTimeout);
            _getInfoMethod = new CloudToDeviceMethod("GetInfo", responseTimeout, connectionTimeout);
        }

        /// <summary>
        /// Call the GetDiagnosticInfo method.
        /// </summary>
        public async Task<DiagnosticInfoMethodResponseModel> GetDiagnosticInfoAsync(CancellationToken ct)
        {
            DiagnosticInfoMethodResponseModel response = null;

            try
            {
                CloudToDeviceMethodResult methodResult = new CloudToDeviceMethodResult();
                if (string.IsNullOrEmpty(_publisherModuleName))
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _getDiagnosticInfoMethod, ct);
                }
                else
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _publisherModuleName, _getDiagnosticInfoMethod, ct);
                }
                if (methodResult.Status == (int)HttpStatusCode.OK)
                {
                    response = JsonConvert.DeserializeObject<DiagnosticInfoMethodResponseModel>(methodResult.GetPayloadAsJson());
                }
                else
                {
                    Logger.Error($"GetDiagnosticInfo failed with status {methodResult.Status}");
                }
            }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                {
                    Logger.Fatal(e, $"GetDiagnosticInfo exception");
                }
            }
            return response;
        }

        /// <summary>
        /// Call the GetDiagnosticLog method.
        /// </summary>
        public async Task<DiagnosticLogMethodResponseModel> GetDiagnosticLogAsync(CancellationToken ct)
        {
            DiagnosticLogMethodResponseModel response = null;

            try
            {
                CloudToDeviceMethodResult methodResult = new CloudToDeviceMethodResult();
                if (string.IsNullOrEmpty(_publisherModuleName))
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _getDiagnosticLogMethod, ct);
                }
                else
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _publisherModuleName, _getDiagnosticLogMethod, ct);
                }
                if (methodResult.Status == (int)HttpStatusCode.OK)
                {
                    response = JsonConvert.DeserializeObject<DiagnosticLogMethodResponseModel>(methodResult.GetPayloadAsJson());
                }
                else
                {
                    Logger.Error($"GetDiagnosticLog failed with status {methodResult.Status}");
                }
            }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                {
                    Logger.Fatal(e, $"GetDiagnosticLog exception");
                }
            }
            return response;
        }

        /// <summary>
        /// Call the ExitApplication method.
        /// </summary>
        public async Task<bool> ExitApplicationAsync(int secondsTillExit)
        {
            bool result = false;
            Logger.Information("");
            Logger.Information($"Calling OPC Publisher to exit in {SecondsTillExit} seconds.");
            try
            {
                ExitApplicationMethodRequestModel exitApplicationMethodRequestModel = new ExitApplicationMethodRequestModel();
                exitApplicationMethodRequestModel.SecondsTillExit = secondsTillExit;
                _exitApplicationMethod.SetPayloadJson(JsonConvert.SerializeObject(exitApplicationMethodRequestModel));

                CloudToDeviceMethodResult methodResult = new CloudToDeviceMethodResult();
                if (string.IsNullOrEmpty(_publisherModuleName))
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _exitApplicationMethod);
                }
                else
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _publisherModuleName, _exitApplicationMethod);
                }
                if (methodResult.Status == (int)HttpStatusCode.OK)
                {
                    Logger.Information($"OPC Publisher will exit in {SecondsTillExit} seconds.");
                    result = true;
                }
                else
                {
                    Logger.Error($"ExitApplication method failed with status {methodResult.Status}");
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e, $"ExitApplication exception");
            }
            return result;
        }

        /// <summary>
        /// Call the GetInfo method.
        /// </summary>
        public async Task<GetInfoMethodResponseModel> GetInfoAsync(CancellationToken ct)
        {
            GetInfoMethodResponseModel response = null;

            try
            {
                CloudToDeviceMethodResult methodResult = new CloudToDeviceMethodResult();
                if (string.IsNullOrEmpty(_publisherModuleName))
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _getInfoMethod, ct);
                }
                else
                {
                    methodResult = await _iotHubClient.InvokeDeviceMethodAsync(_publisherDeviceName, _publisherModuleName, _getInfoMethod, ct);
                }
                if (methodResult.Status == (int)HttpStatusCode.OK)
                {
                    response = JsonConvert.DeserializeObject<GetInfoMethodResponseModel>(methodResult.GetPayloadAsJson());
                }
                else
                {
                    Logger.Error($"GetInfo failed with status {methodResult.Status}");
                }
            }
            catch (Exception e)
            {
                if (!ct.IsCancellationRequested)
                {
                    Logger.Debug(e, $"GetInfo exception");
                }
            }

            if (response == null && !ct.IsCancellationRequested)
            {
                Logger.Information("");
                Logger.Information($"OPC Publisher is not responding. Either the used version is too old or it is not running.");
                Logger.Information("");
            }

            return response;
        }

        ServiceClient _iotHubClient;
        string _publisherDeviceName;
        Device _publisherDevice;
        string _publisherModuleName;
        Module _publisherModule;
        CloudToDeviceMethod _getDiagnosticInfoMethod;
        CloudToDeviceMethod _getDiagnosticLogMethod;
        CloudToDeviceMethod _exitApplicationMethod;
        CloudToDeviceMethod _getInfoMethod;
    }
}
