using BridgeRT;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace AdapterLib
{
    class StreamAPIReader
    {
        private IDictionary<string, IAdapterDevice> adapterDevices = null;
        Adapter adapter = null;
        public Task readStream(HttpClient m_httpClient, IDictionary<string, IAdapterDevice> adapterDevices, Adapter adapter)
        {
            this.adapterDevices = adapterDevices;
            this.adapter = adapter;
            string DCGWUrl = Adapter.DCGWUrl;
            //This class reads stream API and upate devices states 
            //This will have non ending thread that wil always keep listning from stream API for telegrams

            HttpResponseMessage response = m_httpClient.GetAsync(new Uri(DCGWUrl + "devices/stream"), HttpCompletionOption.ResponseHeadersRead).AsTask().Result;
            IInputStream inputStream = response.Content.ReadAsInputStreamAsync().AsTask().Result;

            ulong totalBytesRead = 0;
            IBuffer buffer = new Windows.Storage.Streams.Buffer(10000000);
            return Task.Run(()=> {
                do
                {
                    buffer = inputStream.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.Partial).AsTask().Result;
                    totalBytesRead += buffer.Length;

                    DataReader dataReader = DataReader.FromBuffer(buffer);
                    var bufferStr = dataReader.ReadString(buffer.Length);
                    Debug.WriteLine("Json..............");
                    Debug.WriteLine(bufferStr);

                    var isJsonValid = ValidateJSON(bufferStr);
                    if (isJsonValid)
                    {
                        var Json = JObject.Parse(bufferStr);
                        var ContentType = Json.First.First.Value<string>("content");
                        if (ContentType.Equals("devices"))
                        {
                            //update devices with their current states...
                            Debug.WriteLine("devices..............");
                            var devices = Json.Value<JToken>("devices");
                            updateDevices(devices);
                        }
                        else
                        if (ContentType.Equals("telegram"))
                        {
                            //update device attribute with its current value...
                            Debug.WriteLine("Telegram..............");
                            var telegram = Json.Value<JToken>("telegram");
                            updateDevice(telegram);
                        }
                        else
                        if (ContentType.Equals("device"))
                        {
                            //update device attribute with its current value...
                            Debug.WriteLine("Device..............");
                            var device = Json.Value<JToken>("device");
                            //device.Value<string>("friendlyId");
                            //device.Value<string>("eep");
                            //device.Value<string>("lastSeen");
                            //device.Value<string>("version");
                            //device.Value<JToken>("transmitModes");
                            string operable = device.Value<string>("operable");
                            if (operable != null && operable.Equals("True"))
                            {
                                //addDevice(device);
                            }
                        }
                    }

                } while (buffer.Length > 0);
            });
        }

        //private void addDevice(JToken device)
        //{
        //    add device to Device list
        //    but how how to determine profile?
        //    adapter.addDevice(device.ToString());
        //}

        private void updateDevices(JToken devicesJT)
        {
            Debug.WriteLine("updateDevicesss.......");
            var devices = devicesJT.Children();
            Debug.WriteLine("Count......" + devices.Count());
            foreach (var deviceToken in devices)
            {
                var deviceId = deviceToken.Value<string>("deviceId");
                AdapterDevice device = (AdapterDevice)GetObject(adapterDevices, deviceId);
                //if (device != null)
                //{
                    var functions = deviceToken.Value<JToken>("states");
                    foreach (var funcntion in functions)
                    {
                        var key = funcntion.Value<string>("key");
                        var value = funcntion.Value<string>("value");
                        var meaning = funcntion.Value<string>("meaning");

                        IList<IAdapterProperty> properties = device.Properties;
                        foreach (var property in properties)
                        {
                            IList<IAdapterAttribute> attributes = property.Attributes;
                            foreach (var attribute in attributes)
                            {
                                if (attribute.Value.Name.Equals(key))
                                {
                                    attribute.Value.Data = Windows.Foundation.PropertyValue.CreateString(value);
                                }
                            }
                        }
                   // }
                }
            }
        }

        private void updateDevice(JToken telegram)
        {
            Debug.WriteLine("updateDevice.......");
            var deviceId = telegram.Value<string>("deviceId");
            var direction = telegram.Value<string>("direction");
            var functions = telegram.Value<JToken>("functions");
            foreach (var funcntion in functions)
            {
                var key = funcntion.Value<string>("key");
                var value = funcntion.Value<string>("value");
                var meaning = funcntion.Value<string>("meaning");

                AdapterDevice device = (AdapterDevice)GetObject(adapterDevices, deviceId);

                if (direction.Equals("from"))
                {
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
                                    adapter.NotifySignalListener(covSignal);
                                }
                            }
                        }
                    }
                }
            }
        }

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
                JObject.Parse(s);
                return true;
            }
            catch (JsonReaderException ex)
            {
                Debug.WriteLine(ex.StackTrace);
                return false;
            }
        }
    }
}
