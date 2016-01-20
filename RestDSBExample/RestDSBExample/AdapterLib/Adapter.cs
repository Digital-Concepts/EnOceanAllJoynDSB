using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using BridgeRT;
using Windows.Web.Http.Filters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Windows.Web.Http.Headers;
using Windows.Web.Http;
using System.Diagnostics;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.Storage;
using Microsoft.Win32;
using System.Net;
using Windows.Storage.Streams;

namespace AdapterLib
{
    public sealed class Adapter : IAdapter
    {
        // Connection strings variable for EnOcean APIs
        public static string DCGWUrl { get; set; }
        private HttpClient httpClient;

        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_INVALID_HANDLE = 6;

        // Device Arrival and Device Removal Signal Indices
        private const int DEVICE_ARRIVAL_SIGNAL_INDEX = 0;
        private const int DEVICE_ARRIVAL_SIGNAL_PARAM_INDEX = 0;
        private const int DEVICE_REMOVAL_SIGNAL_INDEX = 1;
        private const int DEVICE_REMOVAL_SIGNAL_PARAM_INDEX = 0;

        public string Vendor { get; }
        public string AdapterName { get; }
        public string Version { get; }
        public string ExposedAdapterPrefix { get; }
        public string ExposedApplicationName { get; }
        public Guid ExposedApplicationGuid { get; }
        public IList<IAdapterSignal> Signals { get; }

        public Adapter()
        {
            Windows.ApplicationModel.Package package = Windows.ApplicationModel.Package.Current;
            Windows.ApplicationModel.PackageId packageId = package.Id;
            Windows.ApplicationModel.PackageVersion versionFromPkg = packageId.Version;

            this.Vendor = "EnOcean";
            this.AdapterName = "Bridge";

            // the adapter prefix must be something like "com.mycompany" (only alpha num and dots)
            // it is used by the Device System Bridge as root string for all services and interfaces it exposes
            this.ExposedAdapterPrefix = "com." + this.Vendor.ToLower();
            this.ExposedApplicationGuid = Guid.Parse("{0x0b2b3b87,0xc1fc,0x4282,{0x96,0xad,0x88,0xc7,0x90,0xaf,0x15,0xe3}}");

            if (null != package && null != packageId)
            {
                this.ExposedApplicationName = packageId.Name;
                this.Version = versionFromPkg.Major.ToString() + "." +
                               versionFromPkg.Minor.ToString() + "." +
                               versionFromPkg.Revision.ToString() + "." +
                               versionFromPkg.Build.ToString();
            }
            else
            {
                this.ExposedApplicationName = "EnOcean Alljoyn DSB";
                this.Version = "0.0.0.0";
            }

            try
            {
                this.Signals = new List<IAdapterSignal>();
                this.devices = new List<IAdapterDevice>();
                this.devicesDict = new Dictionary<string, IAdapterDevice>();
                this.SignalsDict = new Dictionary<string, IAdapterSignal>();
                this.signalListeners = new Dictionary<int, IList<SIGNAL_LISTENER_ENTRY>>();

                //Create Adapter Signals
                this.createSignals();
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }           
        }

        public uint SetConfiguration([ReadOnlyArray] byte[] ConfigurationData)
        {
            return ERROR_SUCCESS;
        }

        public uint GetConfiguration(out byte[] ConfigurationDataPtr)
        {
            ConfigurationDataPtr = null;

            return ERROR_SUCCESS;
        }

        public uint Initialize()
        {
            setDCGURL();                   
            var filter = new HttpBaseProtocolFilter();
            try
            {
                filter.ServerCredential = new Windows.Security.Credentials.PasswordCredential(DCGWUrl, "admin", "admin");
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine(ex.ParamName);
            }

            httpClient = new HttpClient(filter);
            return ERROR_SUCCESS;
        }

        private void setDCGURL()
        {
            //var sf = await Package.Current.InstalledLocation.TryGetItemAsync("ipaddress.txt") as StorageFile;
            //Adapter.DCGWUrl = await Windows.Storage.FileIO.ReadTextAsync(sf).AsTask<string>();
            //Adapter.DCGWUrl = System.IO.File.ReadAllText(@"C:\Data\Users\DefaultAccount\ipaddress.txt");
            DCGWUrl = "http://172.28.28.51:8080/";
            //DCGWUrl = "http:/dcgw.enocean-gateway.eu:8080/"
            //try {
            //    WebRequest request = WebRequest.CreateHttp("http:/www.enocean-gateway.de/iot/ip.txt");
            //    WebResponse response = request.GetResponseAsync().Result;
            //    Stream dataStream = response.GetResponseStream();
            //    StreamReader reader = new StreamReader(dataStream);
            //    DCGWUrl = reader.ReadLine();
            //}
            //catch (Exception ex) {
            //    Debug.WriteLine(ex.Message);
            //} 
        }

        public uint Shutdown()
        {
            return ERROR_SUCCESS;
        }

        public uint EnumDevices(ENUM_DEVICES_OPTIONS Options, out IList<IAdapterDevice> DeviceListPtr, out IAdapterIoRequest RequestPtr)
        {
            RequestPtr = null;
            this.PopulateDevicesAsync();

            try
            {
                DeviceListPtr = new List<IAdapterDevice>(this.devices);
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
            return ERROR_SUCCESS;
        }

        public uint GetProperty(IAdapterProperty Property, out IAdapterIoRequest RequestPtr)
        {
            RequestPtr = null;
            return ERROR_SUCCESS;
        }

        public uint SetProperty(IAdapterProperty Property, out IAdapterIoRequest RequestPtr)
        {
            RequestPtr = null;
            return ERROR_SUCCESS;
        }

        public uint GetPropertyValue(IAdapterProperty Property, string AttributeName, out IAdapterValue ValuePtr, out IAdapterIoRequest RequestPtr)
        {
            ValuePtr = null;
            RequestPtr = null;

            // find corresponding attribute
            foreach (var attribute in ((AdapterProperty)Property).Attributes)
            {
                if (attribute.Value.Name == AttributeName)
                {
                    ValuePtr = attribute.Value;
                    return ERROR_SUCCESS;
                }
            }
            return ERROR_INVALID_HANDLE;
        }

        public uint SetPropertyValue(IAdapterProperty Property, IAdapterValue Value, out IAdapterIoRequest RequestPtr)
        {
            RequestPtr = null;

            // find corresponding attribute
            foreach (var attribute in ((AdapterProperty)Property).Attributes)
            {
                if (attribute.Value.Name == Value.Name)
                {
                    string path = "devices/" + ((AdapterValue)attribute.Value).Path + "/state";
                    uint status = this.SetHttpValue(path, attribute.Value.Data, attribute.Value.Name);
                    if (status == 0)
                    {
                        attribute.Value.Data = Value.Data;
                        IAdapterDevice adapterDevice = null;
                        this.devicesDict.TryGetValue(((AdapterValue)Value).Path, out adapterDevice);
                        int SignalHashCode = ((AdapterValue)attribute.Value).SignalHashCode;
                        IAdapterSignal covSignal = null;
                        ((AdapterDevice)adapterDevice).SignalsDict.TryGetValue(SignalHashCode, out covSignal);
                        this.NotifySignalListener(covSignal);
                    }

                    return ERROR_SUCCESS;
                }
            }
            return ERROR_INVALID_HANDLE;
        }

        public uint SetPropertyValue(IAdapterProperty Property, IAdapterValue Value, IAdapterSignal covSignal)
        {
            // find corresponding attribute
            foreach (var attribute in ((AdapterProperty)Property).Attributes)
            {
                if (attribute.Value.Name == Value.Name)
                {
                    //this.SetHttpValue(((AdapterValue)attribute.Value).Path, attribute.Value.Data, attribute.Value.Name);
                    attribute.Value.Data = Value.Data;

                    foreach (IAdapterValue param in covSignal.Params)
                    {
                        if (param.Name == Constants.COV__ATTRIBUTE_HANDLE)
                        {
                            IAdapterValue valueAttr_Value = (IAdapterValue)param.Data;
                            if (valueAttr_Value.Name == Value.Name)
                            {
                                param.Data = Value.Data;
                                NotifySignalListener(covSignal);
                            }
                        }
                    }

                    return ERROR_SUCCESS;
                }
            }
            return ERROR_INVALID_HANDLE;
        }

        public uint CallMethod(IAdapterMethod Method, out IAdapterIoRequest RequestPtr)
        {
            RequestPtr = null;
            AdapterMethod adapterMethod = Method as AdapterMethod;
            string path = ((AdapterValue)Method.InputParams.First()).Path;
            IList<IAdapterValue> inputParams = Method.InputParams;
            string functions = null;
            foreach (var inputParam in inputParams)
            {
                var key = inputParam.Name;
                var value = (string)inputParam.Data;
                if (functions == null)
                {
                    functions = "{\"key\" : \"" + key + "\",\"value\" : \"" + value + "\"}";
                }
                else {
                    functions += ",{\"key\" : \"" + key + "\",\"value\" : \"" + value + "\"}";
                }

            }
            //string deviceId = adapterMethod.Path;
            //string path = "devices/"+adapterMethod.Path+"/state";

            ////IAdapterDevice outAdapterDevice = null;
            ////devicesDict.TryGetValue(deviceId, out outAdapterDevice);
            ////AdapterDevice adapterDevice = outAdapterDevice as AdapterDevice;

            ////string [] keyValue = adapterDevice.getKeyValue(adapterMethod);

            //string[] keyValue = Method.Name.Split('_');
            //string key = keyValue[0];
            //string data = keyValue[1];

            //return (uint)SetHttpValue(path, data, key);
            return (uint)SetHttpValue(path, functions);
        }

        public uint RegisterSignalListener(IAdapterSignal Signal, IAdapterSignalListener Listener, object ListenerContext)
        {
            if (Signal == null || Listener == null)
            {
                return ERROR_INVALID_HANDLE;
            }

            int signalHashCode = Signal.GetHashCode();

            SIGNAL_LISTENER_ENTRY newEntry;
            newEntry.Signal = Signal;
            newEntry.Listener = Listener;
            newEntry.Context = ListenerContext;

            lock (this.signalListeners)
            {
                if (this.signalListeners.ContainsKey(signalHashCode))
                {
                    this.signalListeners[signalHashCode].Add(newEntry);
                }
                else
                {
                    IList<SIGNAL_LISTENER_ENTRY> newEntryList;

                    try
                    {
                        newEntryList = new List<SIGNAL_LISTENER_ENTRY>();
                    }
                    catch (OutOfMemoryException ex)
                    {
                        throw;
                    }

                    newEntryList.Add(newEntry);
                    this.signalListeners.Add(signalHashCode, newEntryList);
                }
            }
            return ERROR_SUCCESS;
        }

        public uint UnregisterSignalListener(IAdapterSignal Signal, IAdapterSignalListener Listener)
        {
            return ERROR_SUCCESS;
        }

        public uint NotifySignalListener(IAdapterSignal Signal)
        {
            if (Signal == null)
            {
                return ERROR_INVALID_HANDLE;
            }

            int signalHashCode = Signal.GetHashCode();

            lock (this.signalListeners)
            {
                IList<SIGNAL_LISTENER_ENTRY> listenerList = this.signalListeners[signalHashCode];
                foreach (SIGNAL_LISTENER_ENTRY entry in listenerList)
                {
                    IAdapterSignalListener listener = entry.Listener;
                    object listenerContext = entry.Context;
                    listener.AdapterSignalHandler(Signal, listenerContext);
                }
            }
            return ERROR_SUCCESS;
        }

        public uint NotifyDeviceArrival(IAdapterDevice Device)
        {
            if (Device == null)
            {
                return ERROR_INVALID_HANDLE;
            }

            IAdapterSignal deviceArrivalSignal = this.Signals[DEVICE_ARRIVAL_SIGNAL_INDEX];
            IAdapterValue signalParam = deviceArrivalSignal.Params[DEVICE_ARRIVAL_SIGNAL_PARAM_INDEX];
            signalParam.Data = Device;
            this.NotifySignalListener(deviceArrivalSignal);

            return ERROR_SUCCESS;
        }

        public uint NotifyDeviceRemoval(IAdapterDevice Device)
        {
            if (Device == null)
            {
                return ERROR_INVALID_HANDLE;
            }

            IAdapterSignal deviceRemovalSignal = this.Signals[DEVICE_REMOVAL_SIGNAL_INDEX];
            IAdapterValue signalParam = deviceRemovalSignal.Params[DEVICE_REMOVAL_SIGNAL_PARAM_INDEX];
            signalParam.Data = Device;
            this.NotifySignalListener(deviceRemovalSignal);

            return ERROR_SUCCESS;
        }

        private void createSignals()
        {
            try
            {
                // Device Arrival Signal
                AdapterSignal deviceArrivalSignal = new AdapterSignal(Constants.DEVICE_ARRIVAL_SIGNAL);
                AdapterValue deviceHandle_arrival = new AdapterValue(
                                                            Constants.DEVICE_ARRIVAL__DEVICE_HANDLE,
                                                            null);
                deviceArrivalSignal.Params.Add(deviceHandle_arrival);

                // Device Removal Signal
                AdapterSignal deviceRemovalSignal = new AdapterSignal(Constants.DEVICE_REMOVAL_SIGNAL);
                AdapterValue deviceHandle_removal = new AdapterValue(
                                                            Constants.DEVICE_REMOVAL__DEVICE_HANDLE,
                                                            null);
                deviceRemovalSignal.Params.Add(deviceHandle_removal);

                // Add Signals to the Adapter Signals
                this.Signals.Add(deviceArrivalSignal);
                this.Signals.Add(deviceRemovalSignal);
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        private struct SIGNAL_LISTENER_ENTRY
        {
            // The signal object
            internal IAdapterSignal Signal;

            // The listener object
            internal IAdapterSignalListener Listener;

            //
            // The listener context that will be
            // passed to the signal handler
            //
            internal object Context;
        }

        // List of Devices
        private IList<IAdapterDevice> devices;
        private Dictionary<string, IAdapterDevice> devicesDict;
        private Dictionary<string, IAdapterSignal> SignalsDict;
        // A map of signal handle (object's hash code) and related listener entry
        private Dictionary<int, IList<SIGNAL_LISTENER_ENTRY>> signalListeners;

        private Task StartListeningAsync()
        {
            return Task.Run(() =>
            {
                var request = PrepareRequest(HttpMethod.Get, "devices/stream", "", "");
                //var httpClient = new HttpClient(new HttpBaseProtocolFilter());
                var response = httpClient.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead).AsTask().Result;
                if (response.IsSuccessStatusCode)
                {
                    using (response)
                    {
                    using (var stream = response.Content.ReadAsInputStreamAsync().GetResults())
                        {
                            IBuffer buffer = new Windows.Storage.Streams.Buffer(10000000);
                            while (true) {
                                buffer = stream.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial).AsTask().Result;
                                DataReader dataReader = DataReader.FromBuffer(buffer);
                                var bufferStr = dataReader.ReadString(buffer.Length);
                                Debug.WriteLine("buffer.Length.........."+buffer.Length);
                                Debug.WriteLine(bufferStr);
                            }
                        }
                    }
                }
                            
                           // using (var reader = new DataReader(stream))
                            //{
                //                string eventName = null;

                //                while (true)
                //                {
                //                    var read = reader.ReadString(reader.UnconsumedBufferLength);

                //                    Debug.WriteLine(read);

                //                    if (read.StartsWith("event: "))
                //                    {
                //                        eventName = read.Substring(7);
                //                        continue;
                //                    }

                //                    if (read.StartsWith("data: "))
                //                    {
                //                        if (string.IsNullOrEmpty(eventName))
                //                        {
                //                            throw new InvalidOperationException("Payload data was received but an event did not preceed it.");
                //                        }

                //                        //Update(eventName, read.Substring(6));
                //                    }

                //                    // start over
                //                    eventName = null;
                //                }
                //            }
                //        }
                //    }
                //}
            });
        }

        private Task<HttpResponseMessage> GetAsyncRequest(string uri)
        {
            return httpClient.GetAsync(new Uri(uri)).AsTask();
        }

        private async Task<HttpResponseMessage> GetDevices(string uri)
        {

            return await httpClient.GetAsync(new Uri(DCGWUrl + "devices")).AsTask();
        }
        private Task PopulateDevicesAsync()
        {
            try
            {
                return httpClient.GetAsync(new Uri(DCGWUrl + "devices")).AsTask().ContinueWith((response) =>
                {
                    if (response.Result.IsSuccessStatusCode)
                    {
                        var body = response.Result.Content.ReadAsStringAsync().AsTask().Result;
                        var devices = JObject.Parse(body).Value<JToken>("devices");

                        foreach (var device in devices)
                        {
                            string deviceId = device.Value<string>("deviceId");
                            string friendlyId = device.Value<string>("friendlyId");

                            response = httpClient.GetAsync(new Uri(DCGWUrl + "devices/" + deviceId)).AsTask();
                            body = response.Result.Content.ReadAsStringAsync().AsTask().Result;
                            string eep = JObject.Parse(body).Value<JToken>("device").Value<string>("eep");

                            response = httpClient.GetAsync(new Uri(DCGWUrl + "profiles/" + eep)).AsTask();
                            body = response.Result.Content.ReadAsStringAsync().AsTask().Result;

                            var profile = JObject.Parse(body).Value<JToken>("profile");
                            var title = profile.Value<string>("title");
                            var functionGroups = profile.Value<JToken>("functionGroups");

                            var adapterDevice = new AdapterDevice(friendlyId, "EnOcean", eep, "0", deviceId, title, "");

                            foreach (var functionGroup in functionGroups)
                            {
                                var titleFG = functionGroup.Value<string>("title");
                                var direction = functionGroup.Value<string>("direction");
                                var functions = functionGroup.Value<JToken>("functions");

                                titleFG = titleFG != null ? titleFG : "Property";
                                AdapterProperty property = new AdapterProperty(titleFG, "");

                                foreach (var function in functions)
                                {
                                    var key = function.Value<string>("key");
                                    var keyDescription = function.Value<string>("description");
                                    var defaultValue = function.Value<string>("defaultValue");

                                    var values = function.Value<JToken>("values");
                                    string value = null;
                                    string min = null;
                                    string max = null;
                                    string meaning = null;


                                    if (defaultValue == null && values != null)
                                    {
                                        foreach (var valueJT in values)
                                        {
                                            defaultValue = valueJT.Value<string>("value");
                                            var valueKey = valueJT.Value<string>("valueKey");
                                            meaning = valueJT.Value<string>("meaning");

                                            var range = valueJT.Value<JToken>("range");
                                            if (range != null)
                                            {
                                                string step = null;
                                                string unit = null;

                                                defaultValue = range.Value<string>("min");
                                                max = range.Value<string>("max");
                                                step = range.Value<string>("step");
                                                unit = range.Value<string>("unit");
                                            }
                                            break;
                                        }
                                    }
                                    else {
                                        var range = function.Value<JToken>("range");
                                        if (range != null)
                                        {
                                            string step = null;
                                            string unit = null;

                                            defaultValue = range.Value<string>("min");
                                            max = range.Value<string>("max");
                                            step = range.Value<string>("step");
                                            unit = range.Value<string>("unit");
                                        }
                                    }

                                    object valueData = Windows.Foundation.PropertyValue.CreateString(defaultValue);
                                    AdapterAttribute valueAttr = valueAttr = new AdapterAttribute(key, valueData, deviceId, E_ACCESS_TYPE.ACCESS_READWRITE);
                                    if (direction.Equals("from"))
                                    {
                                        valueAttr = new AdapterAttribute(key, valueData, deviceId, E_ACCESS_TYPE.ACCESS_READ);
                                    }
                                    else if (direction.Equals("both"))
                                    {
                                        object valueDataTest = Windows.Foundation.PropertyValue.CreateString("");
                                        //This is a workaround to know if device supports both functionality                                        
                                        //500 will be response status for read only property and 400 for device that support both direct, status is 400 because we are sending no value
                                        uint status = SetHttpValue("devices/" + deviceId + "/state", valueDataTest, key);
                                        if (status == 500)
                                        {
                                            valueAttr = new AdapterAttribute(key, valueData, deviceId, E_ACCESS_TYPE.ACCESS_READ);
                                        }
                                    }
                                    valueAttr.COVBehavior = SignalBehavior.Always;
                                    adapterDevice.AddChangeOfValueSignal(property, valueAttr.Value);
                                    property.Attributes.Add(valueAttr);
                                }
                                adapterDevice.Properties.Add(property);
                            }

                            this.devicesDict.Add(deviceId, adapterDevice);
                            this.NotifyDeviceArrival(adapterDevice);
                        }
                        this.devices = devicesDict.Values.ToList();
                    }
                    StreamAPIReader streamReader = new StreamAPIReader();
                    streamReader.readStream(httpClient, devicesDict, this);
                    StartListeningAsync();
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine("PopulateDevice:{0} Exception caught......", e);
                return null;
            }


            //return m_httpClient.GetAsync(new Uri($"{DCGWUrl}devices.json?auth={AccessToken}")).AsTask().ContinueWith( (response) => 
            //{
            //    if (response.Result.IsSuccessStatusCode)
            //    {
            //        var body = response.Result.Content.ReadAsStringAsync().AsTask().Result;
            //        foreach (var devicesKvp in JObject.Parse(body))
            //        {
            //            foreach (var deviceKvp in devicesKvp.Value)
            //            {
            //                var deviceId = deviceKvp.Children()["device_id"].First().Value<string>();
            //                var model = this.CamelCase(devicesKvp.Key.TrimEnd('s'));
            //                var adapterDevice = new AdapterDevice(
            //                        deviceKvp.Children()["name"].First().Value<string>(),
            //                        "Nest",
            //                        model,
            //                        "0.7",
            //                        deviceId,
            //                        deviceKvp.Children()["name_long"].First().Value<string>(),
            //                        AccessToken);

            //                var category = devicesKvp.Key;
            //                var propertiesJson = deviceKvp.First().ToString();

            //                this.AddProperties(category, propertiesJson, deviceId, adapterDevice);
            //                this.AddMethods(category, deviceId, adapterDevice);

            //                this.devices.Add(adapterDevice);
            //                this.NotifyDeviceArrival(adapterDevice);
            //            }
            //        }
            //    }
            //});          
        }

        private string CamelCase(string value)
        {
            var result = string.Empty;
            var tokens = value.Split('_');
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                result += char.ToUpper(token[0]) + token.Substring(1);
            }
            return result;
        }

        private void AddMethods(string category, string deviceId, AdapterDevice adapterDevice)
        {
            var method = new AdapterMethod(
                "SetTemperature",
                "Change the target temperature.",
                0, null);

            var methodPath = $"devices/{category}/{deviceId}";
            //method.InputParams.Add(new AdapterValue("TargetTemperature", 0.0, methodPath));
            //method.InputParams.Add(new AdapterValue("TemperatureScale", 'f'));

            adapterDevice.Methods.Add(method);
        }

        private void AddMethods(string deviceId, AdapterDevice adapterDevice, string methodName, string description, IList<IAdapterValue> InputParams)
        {
            var method = new AdapterMethod(
                methodName,
                description,
                0, deviceId);

            var methodPath = $"devices/{deviceId}/state";
            foreach (var inputParam in InputParams)
            {
                method.InputParams.Add(inputParam);
            }
            adapterDevice.Methods.Add(method);
        }

        private void AddProperties(string category, string propertiesJson, string deviceId, AdapterDevice adapterDevice)
        {
            //var property = new AdapterProperty("Thermostat", string.Empty);

            //adapterDevice.Properties.Add(property);

            //foreach (var propertyKvp in JObject.Parse(propertiesJson))
            //{
            //    var name = this.CamelCase(propertyKvp.Key);
            //    var path = $"devices/{category}/{deviceId}/{propertyKvp.Key}";
            //    object value = this.GetJTokenValue(propertyKvp.Value);

            //    property.Attributes.Add(new AdapterAttribute(name, value, path));
            //}

        }

        private object GetJTokenValue(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                    return token.Value<int>();
                case JTokenType.Float:
                    return token.Value<double>();
                case JTokenType.String:
                default:
                    return token.Value<string>();
            }
        }

        public uint SetHttpValue(string path, object data, string valueName)
        {
            var request = this.PrepareRequest(HttpMethod.Put, path, data, valueName);
            var response = httpClient.SendRequestAsync(request).AsTask().Result;
            Windows.Web.Http.HttpStatusCode statusCode = response.StatusCode;
            if (response.StatusCode == Windows.Web.Http.HttpStatusCode.InternalServerError)
            {
                return 500;
            }
            else if (response.StatusCode == Windows.Web.Http.HttpStatusCode.BadRequest)
            {
                return 400;
            }
            if (response.IsSuccessStatusCode)
            {
                return 0;
            }

            return 1;
        }
        public uint SetHttpValue(string path, string functions)
        {
            var request = this.PrepareRequest(HttpMethod.Put, path, null, functions);
            var response = httpClient.SendRequestAsync(request).AsTask().Result;


            if (response.IsSuccessStatusCode)
            {
                return 0;
            }

            return 1;
        }

        private HttpRequestMessage PrepareRequest(HttpMethod method, string path, object payload, string valueName)
        //private HttpRequestMessage PrepareRequest(HttpMethod method, string path, string functions)
        {
            var uri = this.PrepareUri(path);

            var request = new HttpRequestMessage(method, uri);

            if (payload != null)
            {
                //JSON Data to update Enocean device property
                string payloadString = "{\"state\" : {\"functions\" : [{\"key\" : \"" + valueName + "\",\"value\" : \"" + payload.ToString() + "\"}]}}";
                var json = JsonConvert.ToString(payloadString);
                request.Content = new HttpStringContent(payloadString);
            }

            return request;
        }

        private Uri PrepareUri(string path)
        {
            //var authToken = string.Format("{0}.json?auth={1}", path, "");
            var url = string.Format("{0}", DCGWUrl + path);
            return new Uri(url);
        }
    }
}
