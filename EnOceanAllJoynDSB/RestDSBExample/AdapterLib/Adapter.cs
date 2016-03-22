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

        //This refer to DsbBridge in BridegRT project
        private DsbBridge dsbBridge = null;
        
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

        public Adapter(string DCGWURLParam)
        {

            //setting IP of gateway from UI             
            DCGWUrl = "http://" + DCGWURLParam + ":8080/";

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

        //Setting DsBBridge for the purpose of adding and removing devices when bridge is running
        public void setDsbBridge(DsbBridge dsbBridge) {
            this.dsbBridge = dsbBridge;
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
            var filter = new HttpBaseProtocolFilter();
            try
            {
                filter.ServerCredential = new Windows.Security.Credentials.PasswordCredential(DCGWUrl, "admin", "admin");
                httpClient = new HttpClient(filter);
            }
            catch (ArgumentNullException ex)
            {
                Debug.WriteLine("ArgumentNullException:" + ex.ParamName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception" + ex.Message);
            }

            return ERROR_SUCCESS;
        }

        //private void setDCGURL()
        //{
        //    //var sf = await Package.Current.InstalledLocation.TryGetItemAsync("ipaddress.txt") as StorageFile;
        //    //Adapter.DCGWUrl = await Windows.Storage.FileIO.ReadTextAsync(sf).AsTask<string>();
        //    //Adapter.DCGWUrl = System.IO.File.ReadAllText(@"C:\Data\Users\DefaultAccount\ipaddress.txt");
        //    DCGWUrl = "http://172.28.28.51:8080/";
        //    //DCGWUrl = "http:/dcgw.enocean-gateway.eu:8080/"
        //    //try {
        //    //    WebRequest request = WebRequest.CreateHttp("http:/www.enocean-gateway.de/iot/ip.txt");
        //    //    WebResponse response = request.GetResponseAsync().Result;
        //    //    Stream dataStream = response.GetResponseStream();
        //    //    StreamReader reader = new StreamReader(dataStream);
        //    //    DCGWUrl = reader.ReadLine();
        //    //}
        //    //catch (Exception ex) {
        //    //    Debug.WriteLine(ex.Message);
        //    //} 
        //}

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

        //Currently There is no action supported by brdige
        public uint CallMethod(IAdapterMethod Method, out IAdapterIoRequest RequestPtr)
        {
            RequestPtr = null;
            //AdapterMethod adapterMethod = Method as AdapterMethod;
            //string path = ((AdapterValue)Method.InputParams.First()).Path;
            //IList<IAdapterValue> inputParams = Method.InputParams;
            //string functions = null;
            //foreach (var inputParam in inputParams)
            //{
            //    var key = inputParam.Name;
            //    var value = (string)inputParam.Data;
            //    if (functions == null)
            //    {
            //        functions = "{\"key\" : \"" + key + "\",\"value\" : \"" + value + "\"}";
            //    }
            //    else {
            //        functions += ",{\"key\" : \"" + key + "\",\"value\" : \"" + value + "\"}";
            //    }

            //}
            //return (uint)SetHttpValue(path, functions);
            return 0;
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

        //These Dictionary are added so objects can be get using a key
        private Dictionary<string, IAdapterDevice> devicesDict;
        private Dictionary<string, IAdapterSignal> SignalsDict;

        // A map of signal handle (object's hash code) and related listener entry
        private Dictionary<int, IList<SIGNAL_LISTENER_ENTRY>> signalListeners;

        // This thread Listen for any change in Stream API 
        private Task ReadStreamAsync()
        {
            return Task.Run(() =>
            {
                var request = PrepareRequest(HttpMethod.Get, "devices/stream", "", "");
                var response = httpClient.SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead).AsTask().Result;
                if (response.IsSuccessStatusCode)
                {
                    using (response)
                    {
                        using (var stream = response.Content.ReadAsInputStreamAsync().GetResults())
                        {
                            IBuffer buffer = new Windows.Storage.Streams.Buffer(10000);
                            string bufferStr = "";

                            while (true) {
                                buffer = stream.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial).AsTask().Result;
                                DataReader dataReader = DataReader.FromBuffer(buffer);
                                bufferStr += dataReader.ReadString(buffer.Length);
                                
                                var isJsonValid = ValidateJSON(bufferStr);
                                Debug.WriteLine(isJsonValid + ":" + bufferStr);
                                if (isJsonValid)
                                {
                                    var Json = JObject.Parse(bufferStr);
                                    bufferStr = "";


                                    var ContentType = Json.First.First.Value<string>("content");
                                    if (ContentType.Equals("devices"))
                                    {
                                        var devices = Json.Value<JToken>("devices");
                                        updateDevices(devices);
                                    }
                                    else
                                    if (ContentType.Equals("telegram"))
                                    {
                                        var telegram = Json.Value<JToken>("telegram");
                                        updateDevice(telegram);
                                    }
                                    else
                                    if (ContentType.Equals("device"))
                                    {
                                        var device = Json.Value<JToken>("device");
                                        var deviceId = device.Value<string>("deviceId");
                                        var deleted = device.Value<string>("deleted");
                                        //device.Value<string>("friendlyId");
                                        //device.Value<string>("eep");
                                        //device.Value<string>("lastSeen");
                                        //device.Value<string>("version");
                                        //device.Value<JToken>("transmitModes");
                                        string operable = device.Value<string>("operable");
                                        if (operable != null && operable.Equals("True"))
                                        {
                                            addDevice(device.ToString(), true);
                                        } else if (deleted != null && deleted.Equals("True")) {
                                            deleteDevice((AdapterDevice)GetObject(devicesDict, deviceId));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }                
            });
        }

        private Task PopulateDevicesAsync()
        {
            try
            {
                return httpClient.GetAsync(new Uri(DCGWUrl + "devices")).AsTask().ContinueWith(async (response) =>
                {
                    if (response.Result.IsSuccessStatusCode)
                    {
                        var body = response.Result.Content.ReadAsStringAsync().AsTask().Result;
                        var devices = JObject.Parse(body).Value<JToken>("devices");

                        foreach (var device in devices)
                        {
                            this.addDevice(device.ToString(), false);
                        }
                        this.devices = devicesDict.Values.ToList();
                    }
                    await ReadStreamAsync();
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine("PopulateDevice:{0} Exception caught......", e);
                return null;
            }            
        }

        //private string CamelCase(string value)
        //{
        //    var result = string.Empty;
        //    var tokens = value.Split('_');
        //    for (int i = 0; i < tokens.Length; i++)
        //    {
        //        var token = tokens[i];
        //        result += char.ToUpper(token[0]) + token.Substring(1);
        //    }
        //    return result;
        //}
       

        //No method is added for this bridge
        //private void AddMethods(string deviceId, AdapterDevice adapterDevice, string methodName, string description, IList<IAdapterValue> InputParams)
        //{
        //    var method = new AdapterMethod(
        //        methodName,
        //        description,
        //        0, deviceId);

        //    var methodPath = $"devices/{deviceId}/state";
        //    foreach (var inputParam in InputParams)
        //    {
        //        method.InputParams.Add(inputParam);
        //    }
        //    adapterDevice.Methods.Add(method);
        //}

        //private void AddProperties(string category, string propertiesJson, string deviceId, AdapterDevice adapterDevice)
        //{
        //   

        //}

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
            var request = PrepareRequest(HttpMethod.Put, path, data, valueName);
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
        //public uint SetHttpValue(string path, string functions)
        //{
        //    var request = this.PrepareRequest(HttpMethod.Put, path, null, functions);
        //    var response = httpClient.SendRequestAsync(request).AsTask().Result;


        //    if (response.IsSuccessStatusCode)
        //    {
        //        return 0;
        //    }

        //    return 1;
        //}

        private HttpRequestMessage PrepareRequest(HttpMethod method, string path, object payload, string valueName)
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
            var url = string.Format("{0}", DCGWUrl + path);
            return new Uri(url);
        }

        public void addDevice(string deviceParam, bool isNew )
        {
            JToken device = JObject.Parse(deviceParam);
            var deviceId = device.Value<string>("deviceId");
            var friendlyId = device.Value<string>("friendlyId");

            Task<HttpResponseMessage> response = httpClient.GetAsync(new Uri(DCGWUrl + "devices/" + deviceId)).AsTask();
            string body = response.Result.Content.ReadAsStringAsync().AsTask().Result;
            var eep = JObject.Parse(body).Value<JToken>("device").Value<string>("eep");

            response = httpClient.GetAsync(new Uri(DCGWUrl + "profiles/" + eep)).AsTask();
            body = response.Result.Content.ReadAsStringAsync().AsTask().Result;

            var profile = JObject.Parse(body).Value<JToken>("profile");
            var title = profile.Value<string>("title");
            var functionGroups = profile.Value<JToken>("functionGroups");

            AdapterDevice adapterDevice = null;

            if (isLampProfile(eep)) {
                 adapterDevice = new Lamp(friendlyId, "EnOcean", eep, "0", deviceId, title);
                ((Lamp)adapterDevice).adapter = this;

            } else {

                 adapterDevice = new AdapterDevice(friendlyId, "EnOcean", eep, "0", deviceId, title);

                foreach (var functionGroup in functionGroups)
                {
                    var titleFG = functionGroup.Value<string>("title");
                    var direction = functionGroup.Value<string>("direction");
                    var functions = functionGroup.Value<JToken>("functions");

                    titleFG = titleFG != null ? titleFG : "Property";
                    var property = new AdapterProperty(titleFG, "");

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
                        var valueAttr = new AdapterAttribute(key, valueData, deviceId, E_ACCESS_TYPE.ACCESS_READWRITE);
                        if (direction.Equals("from"))
                        {
                            valueAttr = new AdapterAttribute(key, valueData, deviceId, E_ACCESS_TYPE.ACCESS_READ);
                        }
                        else if (direction.Equals("both"))
                        {
                            object valueDataTest = Windows.Foundation.PropertyValue.CreateString("");

                            //This is a workaround to know if device supports both functionality                                        
                            //500 is response status for read only property and 400 for device that support both direct, status is 400 because we are sending no value (valueDataTest is emplty string)
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
            }

            this.devicesDict.Add(deviceId, adapterDevice);

            //update device list in bridge if device is added when bridge is running
            if (isNew)
            {
                dsbBridge.UpdateDeviceCustome(adapterDevice, false);
            }

            this.NotifyDeviceArrival(adapterDevice);
            //this.devices = devicesDict.Values.ToList();
        }

        private void deleteDevice(AdapterDevice adapterDevice)
        {
            dsbBridge.UpdateDeviceCustome(adapterDevice, true);
            //test
            devicesDict.Remove(adapterDevice.SerialNumber);
        }

        //Update status of all devices with their current values 
        private void updateDevices(JToken devicesJT)
        {
            var devices = devicesJT.Children();
            foreach (var deviceToken in devices)
            {
                var deviceId = deviceToken.Value<string>("deviceId");
                AdapterDevice device = (AdapterDevice)GetObject(devicesDict, deviceId);

                var functions = deviceToken.Value<JToken>("states");
                foreach (var funcntion in functions)
                {
                    var key = funcntion.Value<string>("key");
                    var value = funcntion.Value<string>("value");
                    var meaning = funcntion.Value<string>("meaning");

                    if (isLamp(key)) {
                        ((Lamp)device).updateStates(UInt16.Parse(value));
                        break;
                    };

                    IList<IAdapterProperty> properties = device.Properties;
                    foreach (var property in properties)
                    {
                        IList<IAdapterAttribute> attributes = property.Attributes;
                        foreach (var attribute in attributes)
                        {
                            if (attribute.Value.Name.Equals(key))
                            {
                                if (value != null) {
                                    attribute.Value.Data = Windows.Foundation.PropertyValue.CreateString(value);
                                }                                
                            }
                        }
                    }
                }
            }
        }
       

        //Update status/Property of a single device
        private void updateDevice(JToken telegram)
        {
            
            var deviceId = telegram.Value<string>("deviceId");
            var direction = telegram.Value<string>("direction");
            var functions = telegram.Value<JToken>("functions");
            foreach (var funcntion in functions)
            {
                var key = funcntion.Value<string>("key");
                var value = funcntion.Value<string>("value");
                var meaning = funcntion.Value<string>("meaning");

                AdapterDevice device = (AdapterDevice)GetObject(devicesDict, deviceId);

                if (direction.Equals("from"))
                {
                    if (isLamp(key))
                    {
                        if (value != ((Lamp)device).OnOff_Value_Save.ToString())
                        {
                            ((Lamp)device).updateStates(UInt16.Parse(value));
                        }

                        
                        //AdapterSignal covSignal = null;
                        //((AdapterDevice)device).SignalsDict.TryGetValue(SignalHashCode, out covSignal);
                        //this.NotifySignalListener(covSignal);
                        break;
                    };

                    if (device != null)
                    {
                        IList<IAdapterProperty> properties = device.Properties;
                        foreach (var property in properties)
                        {
                            IList<IAdapterAttribute> attributes = property.Attributes;
                            foreach (var attribute in attributes)
                            {
                                if (attribute.Value.Name.Equals(key))
                                {
                                    attribute.Value.Data = Windows.Foundation.PropertyValue.CreateString(value);

                                    int SignalHashCode = ((AdapterValue)attribute.Value).SignalHashCode;
                                    IAdapterSignal covSignal = null;
                                    ((AdapterDevice)device).SignalsDict.TryGetValue(SignalHashCode, out covSignal);

                                    this.NotifySignalListener(covSignal);
                                }
                            }
                        }
                    }
                }
            }
        }

        //Returns a IAdapter device from Dictionary with specified key
        private IAdapterDevice GetObject(IDictionary<string, IAdapterDevice> devices, string key)
        {
            if (devices.ContainsKey(key))
                return devices[key];
            return null;
        }
                
        private bool ValidateJSON(string s)
        {
            try
            {
                JObject jObject = JObject.Parse(s);
                return true;
            }
            catch (JsonReaderException ex)
            {
                Debug.WriteLine("Invalid Json:"+ex.Message);
                return false;
            }
        }

        private bool isLamp(string keyName)
        {
            if (keyName.Equals("dimValue")) {
                return true;
            }
            return false;
        }

        private bool isLampProfile(string eep)
        {
            if (eep.Equals("D2-01-09") || eep.Equals("A5-38-08"))
            {
                return true;
            }
            return false;
        }
    }
}