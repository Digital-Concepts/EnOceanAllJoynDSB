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

        //This refer to DsbBridge in BridegRT project, 
        private DsbBridge dsbBridge = null;
        
        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_INVALID_HANDLE = 6;

        // Device Arrival and Device Removal Signal Indices
        private const int DEVICE_ARRIVAL_SIGNAL_INDEX = 0;
        private const int DEVICE_ARRIVAL_SIGNAL_PARAM_INDEX = 0;
        private const int DEVICE_REMOVAL_SIGNAL_INDEX = 1;
        private const int DEVICE_REMOVAL_SIGNAL_PARAM_INDEX = 0;

        //Learn in a device fields
        public static string FriendlyName  { get; set; }
        public static string NewDeviceProfile  { get; set; }

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
                this.deviceSignals = new Dictionary<string, IAdapterSignal>();
                this.signalListeners = new Dictionary<int, IList<SIGNAL_LISTENER_ENTRY>>();

                //Create Adapter Signals
                this.createSignals();
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        //Setting DsBBridge for the purpose of adding and removing AllJoyn devices when bridge is running
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
                return ERROR_INVALID_HANDLE;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception" + ex.Message);
                return ERROR_INVALID_HANDLE;
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

            if (Method.Name.Equals("Delete"))
            {
                String path = Method.Path;
                return SetHttpValue(path, null, "");
            }
            else
            {
                FriendlyName = (string)Method.InputParams.First(param => param.Name.Equals("FriendlyName", StringComparison.OrdinalIgnoreCase)).Data;
                //NewDeviceProfile = (string)Method.InputParams.First(param => param.Name.Equals("Profile", StringComparison.OrdinalIgnoreCase)).Data;

                if (Method.Name.Equals("Add_RockerSwitch"))
                {
                    NewDeviceProfile = "F6-02-01";
                }
                else if (Method.Name.Equals("Add_Sensor"))
                {
                    NewDeviceProfile = "F6-05-01";
                }
                else if (Method.Name.Equals("Add_Handle"))
                {
                    NewDeviceProfile = "F6-10-00";
                }
                else if (Method.Name.Equals("Add_Other"))
                {
                    NewDeviceProfile = "other";
                }
                return (uint)SetHttpValue("system/receiveMode", null, "learnMode");
            }
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
        private Dictionary<string, IAdapterSignal> deviceSignals;

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
                            string bufferStream = "";

                            while (true) {
                                buffer = stream.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial).AsTask().Result;
                                DataReader dataReader = DataReader.FromBuffer(buffer);
                                bufferStream += dataReader.ReadString(buffer.Length);
                                
                                var isJsonValid = ValidateJSON(bufferStream);
                                if (isJsonValid)
                                {
                                    var streamJson = JObject.Parse(bufferStream);
                                    var JsonProperty = streamJson.Property("header");                                    
                                    Header header = JsonConvert.DeserializeObject<Header>(JsonProperty.Value.ToString());

                                    bufferStream = "";
                                    var content = header.content;                                    
                                    if (content.Equals("telegram"))
                                    {
                                        TelegramBody telegram = JsonConvert.DeserializeObject<TelegramBody>(streamJson.ToString());
                                        updateDevice(telegram.telegram);
                                    }                                                        
                                    else
                                    if (content.Equals("device"))
                                    {
                                        LearnInTelegram learnInTelegram = JsonConvert.DeserializeObject<LearnInTelegram>(streamJson.ToString());
                                        var device = learnInTelegram.device;
                                        var deviceId = device.deviceId;
                                        var operable = device.operable;
                                        var deleted = device.deleted;

                                        if (!operable && !deleted)
                                        {
                                            if (NewDeviceProfile != null) {
                                                AddEODevice(learnInTelegram);
                                            }                                          
                                        } else if (deleted) {
                                            AdapterDevice DeletedDevice = (AdapterDevice)GetObject(devicesDict, deviceId);
                                            if (DeletedDevice != null) { DeleteDevice(DeletedDevice);}                                            

                                        } else if (operable) {
                                            IList<Device> devices = new List<Device>();
                                            devices.Add(device);
                                            AddDevice(device, true);
                                            UpdateDevices(devices);
                                        }
                                    }
                                    else
                                    if (content.Equals("devices"))
                                    {
                                        StreamDevices devices = JsonConvert.DeserializeObject<StreamDevices>(streamJson.ToString());
                                        UpdateDevices(devices.devices);
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
                        Devices devicesObj = JsonConvert.DeserializeObject<Devices>(JObject.Parse(body).ToString());
                        
                        foreach (var device in devicesObj.devices)
                        {
                            this.AddDevice(device, false);
                        }

                        //Add an extra AllJoyn device for Learn-in process
                        AdapterDevice LearnInEODevice = AddLearnInDevice();                     
                        devicesDict.Add("LearnInEODevice", LearnInEODevice);
                        this.NotifyDeviceArrival(LearnInEODevice);

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

        public void AddEODevice(LearnInTelegram learnInTelegram)
        {

            var device = learnInTelegram.device;

            device.friendlyId = FriendlyName;
            device.operable = true;

            var timestamp = learnInTelegram.header.timestamp;
            learnInTelegram.header.timestamp = timestamp.Remove(timestamp.Length - 3, 1);
            var firstSeen = device.firstSeen;
            device.firstSeen = firstSeen.Remove(firstSeen.Length - 3, 1);

            if (!NewDeviceProfile.Equals("F6-02-01") && !NewDeviceProfile.Equals("other"))
            {
                device.eeps.First().eep = NewDeviceProfile;

                Task<HttpResponseMessage> response = httpClient.GetAsync(new Uri(DCGWUrl + "profiles/" + NewDeviceProfile)).AsTask();
                string body = response.Result.Content.ReadAsStringAsync().AsTask().Result;

                ProfileDefination profileInfo = JsonConvert.DeserializeObject<ProfileDefination>(JObject.Parse(body).ToString());

                var profile = profileInfo.profile;
                var functionGroups = profile.functionGroups;

                device.transmitModes.Clear();

                foreach (var functionGroup in functionGroups)
                {
                    var functions = functionGroup.functions;

                    foreach (var function in functions)
                    {
                        TransmitMode transmitMode = new TransmitMode();   
                        transmitMode.key = function.key;
                        transmitMode.transmitOnConnect = function.transmitOnConnect;
                        transmitMode.transmitOnEvent = function.transmitOnEvent;
                        transmitMode.transmitOnDuplicate = function.transmitOnDuplicate;
                        device.transmitModes.Add(transmitMode);
                    }
                }
                NewDeviceProfile = null;
            }
            SetHttpValue("devices/" + device.deviceId, learnInTelegram, null);
            SetHttpValue("system/receiveMode", null, "normalMode");
        }

        //Add an extra AllJoyn device for Learn-in an EO device   
        private AdapterDevice AddLearnInDevice() {

            AdapterDevice LearnInEODevice = new AdapterDevice("LearnInEODevice", "Digital-Concepts", "", "", "", "");

            IAdapterMethod Add_RockerSwitch = new AdapterMethod("Add_RockerSwitch", "Set EO Gateway to learn in mode", 0, "");
            Add_RockerSwitch.InputParams.Add(new AdapterValue("FriendlyName", "FriendlyName", null));
            LearnInEODevice.Methods.Add(Add_RockerSwitch);

            IAdapterMethod Add_Sensor = new AdapterMethod("Add_Sensor", "Set EO Gateway to learn in mode", 0, "");
            Add_Sensor.InputParams.Add(new AdapterValue("FriendlyName", "FriendlyName", null));
            LearnInEODevice.Methods.Add(Add_Sensor);

            IAdapterMethod Add_Handle = new AdapterMethod("Add_Handle", "Set EO Gateway to learn in mode", 0, "");
            Add_Handle.InputParams.Add(new AdapterValue("FriendlyName", "FriendlyName", null));
            LearnInEODevice.Methods.Add(Add_Handle);

            IAdapterMethod Add_Other = new AdapterMethod("Add_Other", "Set EO Gateway to learn in mode", 0, "");
            Add_Other.InputParams.Add(new AdapterValue("FriendlyName", "FriendlyName", null));
            LearnInEODevice.Methods.Add(Add_Other);

            return LearnInEODevice;
        }
        

    public void AddDevice(Device device, bool isNew )
        {
            var deviceId = device.deviceId;
            var friendlyId = device.friendlyId;
            var manufacturer = device.manufacturer != null ? device.manufacturer : "Manufacturer";
            AdapterDevice adapterDevice = null;

            Task<HttpResponseMessage> response = httpClient.GetAsync(new Uri(DCGWUrl + "devices/" + deviceId)).AsTask();
            string body = response.Result.Content.ReadAsStringAsync().AsTask().Result;

            //DeviceProfiles deviceProifles = JsonConvert.DeserializeObject<DeviceProfiles>(JObject.Parse(body).ToString());
            foreach (var eep in device.eeps) {

                var eepName = eep.eep;
                response = httpClient.GetAsync(new Uri(DCGWUrl + "profiles/" + eepName)).AsTask();
                body = response.Result.Content.ReadAsStringAsync().AsTask().Result;

                ProfileDefination profileInfo = JsonConvert.DeserializeObject<ProfileDefination>(JObject.Parse(body).ToString());
                var profile = profileInfo.profile;
                var functionGroups = profile.functionGroups;
                var title = profile.title != null ? profile.title : "TitleDesciption";
                                

                if (isLampProfile(eepName))
                {
                    adapterDevice = new Lamp(friendlyId, manufacturer, eepName, "0", deviceId, title);
                    ((Lamp)adapterDevice).Adapter = this;
                }
                else {

                    adapterDevice = new AdapterDevice(friendlyId, manufacturer, eepName, "0", deviceId, title);
                    foreach (var functionGroup in functionGroups)
                    {
                        string titleFG = functionGroup.title != null ? functionGroup.title : "Property";
                        string direction = direction = functionGroup.direction; ;
                        var functions = functionGroup.functions;

                        var property = new AdapterProperty(titleFG, "");
                        foreach (var function in functions)
                        {
                            var key = function.key;
                            var description = function.description;
                            var defaultValue = function.defaultValue;

                            var values = function.values;
                            string meaning = null;
                            Range range = null;

                            double min = 0.0;
                            double max = 0.0;
                            double step = 0.0;
                            string unit = null;

                            if (defaultValue == null)
                            {
                                var valueTk = values.First<Value>();
                                meaning = valueTk.meaning;
                                range = valueTk.range;
                                defaultValue = valueTk.value;
                                if (range != null)
                                {
                                    min = range.min;
                                    max = range.max;
                                    step = range.step;
                                    unit = range.unit;
                                    defaultValue = range.min;
                                }
                            }

                            object defaultData = Windows.Foundation.PropertyValue.CreateString(defaultValue.ToString());
                            var valueAttr = new AdapterAttribute(key, defaultData, deviceId, E_ACCESS_TYPE.ACCESS_READWRITE);

                            if (range != null)
                            {
                                valueAttr.Annotations.Add("min", defaultValue.ToString());
                                valueAttr.Annotations.Add("max", max.ToString());
                                valueAttr.Annotations.Add("range", step.ToString());
                                valueAttr.Annotations.Add("unit", unit);
                            }

                            if (direction.Equals("from"))
                            {
                                valueAttr = new AdapterAttribute(key, defaultData, deviceId, E_ACCESS_TYPE.ACCESS_READ);
                            }
                            else if (direction.Equals("both"))
                            {
                                object valueDataTest = Windows.Foundation.PropertyValue.CreateString("");

                                //This is a workaround to know if device supports both functionality                                        
                                //500 is response status for read only property and 400 for device that support both direct, 
                                //status is 400 because we are sending no value (valueDataTest is emplty string)
                                uint status = SetHttpValue("devices/" + deviceId + "/state", valueDataTest, key);
                                if (status == 500)
                                {
                                    valueAttr = new AdapterAttribute(key, defaultData, deviceId, E_ACCESS_TYPE.ACCESS_READ);
                                }
                            }

                            valueAttr.COVBehavior = SignalBehavior.Always;
                            adapterDevice.AddChangeOfValueSignal(property, valueAttr.Value);

                            property.Attributes.Add(valueAttr);
                        }
                        adapterDevice.Properties.Add(property);
                    }
                }
            }

            IAdapterMethod Delete = new AdapterMethod("Delete", "Delete EO device", 0, "devices/" + deviceId);
            adapterDevice.Methods.Add(Delete);

            AdapterDevice AddedDevice = (AdapterDevice)GetObject(devicesDict, deviceId);
            if ( AddedDevice == null)

            {
                this.devicesDict.Add(deviceId, adapterDevice);

                //update device list in the bridge if device is added when bridge is running
                if (isNew)
                {
                    dsbBridge.UpdateDeviceCustome(adapterDevice, false);
                }
                this.NotifyDeviceArrival(adapterDevice);
            }            
        }

        //Update status of all abstract EO devices with their current values 
        private void UpdateDevices(IList<Device> devices)
        {
            foreach (var device in devices)
            {
                var deviceId = device.deviceId;
                AdapterDevice adapterDevice = (AdapterDevice)GetObject(devicesDict, deviceId);

                var states = device.states;
                if (states != null) {
                    foreach (var state in states)
                    {
                        var key = state.key;
                        var value = state.value.ToString();
                        var meaning = state.meaning;

                        if (isLamp(key))
                        {
                            ((Lamp)adapterDevice).updateStates(UInt16.Parse(value));
                            break;
                        };
                        //AdapterValue in AdapterProperty is update with current value
                        IList<IAdapterProperty> properties = adapterDevice.Properties;
                        foreach (var property in properties)
                        {
                            IList<IAdapterAttribute> attributes = property.Attributes;
                            foreach (var attribute in attributes)
                            {
                                if (attribute.Value.Name.Equals(key))
                                {
                                    if (value != null)
                                    {
                                        attribute.Value.Data = Windows.Foundation.PropertyValue.CreateString(value);
                                    }
                                }
                            }
                        }
                    }
                }
                
            }
        }
       

        //Update status/Property of a single EO abstract device
        private void updateDevice(Telegram telegram)
        {
            
            var deviceId = telegram.deviceId;
            var direction = telegram.direction;
            var functions = telegram.functions;
            foreach (var funcntion in functions)
            {
                var key = funcntion.key;
                var value = funcntion.value.ToString();

                AdapterDevice device = (AdapterDevice)GetObject(devicesDict, deviceId);

                if (direction.Equals("from"))
                {
                    if (isLamp(key))
                    {
                        if (value != ((Lamp)device).OnOff_Value_Save.ToString())
                        {
                            ((Lamp)device).updateStates(UInt16.Parse(value));
                        }                        
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

        private void deleteEODevice(String deviceId)
        {
        //    SetHttpValue("devices/" + deviceId, null, "");
        }

        private void DeleteDevice(AdapterDevice adapterDevice)
        {
            dsbBridge.UpdateDeviceCustome(adapterDevice, true);
            devicesDict.Remove(adapterDevice.SerialNumber);
        }

        public uint SetHttpValue(string path, object data, string key)
        {
            HttpRequestMessage request = null;
            if (data != null && key != null)
            {
                request = PrepareRequest(HttpMethod.Put, path, data, key);
            }
            else
            if (path.Equals(("system/receiveMode")))
            {
                request = PrepareLearnInMode(HttpMethod.Post, path, key);
            }
            else if (data != null && key == null)
            {
                request = PrepareAddDeviceRequest(HttpMethod.Post, path, data);
            }
            else if (data == null && key != null)
            {
                request = PrepareRemoveDeviceRequest(HttpMethod.Delete, path, data);
            }

            var response = httpClient.SendRequestAsync(request).AsTask().Result;
            Windows.Web.Http.HttpStatusCode statusCode = response.StatusCode;

            //Check for respsonse statuscode
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

        private HttpRequestMessage PrepareRequest(HttpMethod method, string path, object payload, string key)
        {
            var url = string.Format("{0}", DCGWUrl + path);
            var uri = new Uri(url);
            var request = new HttpRequestMessage(method, uri);
            if (payload != null)
            {
                //JSON Data to update Enocean device property
                string payloadString = "{\"state\" : {\"functions\" : [{\"key\" : \"" +
                    key + "\",\"value\" : \"" + payload.ToString() + "\"}]}}";
                var json = JsonConvert.ToString(payloadString);
                request.Content = new HttpStringContent(payloadString);
            }
            return request;
        }

        private HttpRequestMessage PrepareLearnInMode(HttpMethod method, string path, string mode)
        {

            var url = string.Format("{0}", DCGWUrl + path);
            var uri = new Uri(url);
            var request = new HttpRequestMessage(method, uri);

            if (path != null)
            {
                //JSON Data to set gateway in learn in mode
                string learnInMode = "{ \"header\" : { \"content\" : \"receiveMode\"}, \"receiveMode\" : \""+ mode + "\"} }";

                var json = JsonConvert.ToString(learnInMode);
                request.Content = new HttpStringContent(learnInMode);
            }
            return request;
        }

        private HttpRequestMessage PrepareAddDeviceRequest(HttpMethod method, string path, object data)
        {

            var url = string.Format("{0}", DCGWUrl + path);
            var uri = new Uri(url);
            var request = new HttpRequestMessage(method, uri);

            if (path != null)
            {
                //JSON Data to set gateway in learn in mode
                string json = JsonConvert.SerializeObject((LearnInTelegram)data);
                request.Content = new HttpStringContent(json);
            }
            return request;
        }

        private HttpRequestMessage PrepareRemoveDeviceRequest(HttpMethod method, string path, object data)
        {

            var url = string.Format("{0}", DCGWUrl + path);
            var uri = new Uri(url);
            var request = new HttpRequestMessage(method, uri);

            //if (path != null)
            //{
            //    //JSON Data to set gateway in learn in mode
            //    string learnInMode = "{ \"header\" : { \"content\" : \"devices\"}, \"receiveMode\" : \"" + mode + "\"} }";
            //    string json = JsonConvert.SerializeObject((LearnInTelegram)data);
            //    request.Content = new HttpStringContent(json);
            //}
            return request;
        }

        private Uri PrepareUri(string path)
        {
            var url = string.Format("{0}", DCGWUrl + path);
            return new Uri(url);
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