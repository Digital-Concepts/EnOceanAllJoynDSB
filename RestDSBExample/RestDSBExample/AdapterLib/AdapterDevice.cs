using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BridgeRT;

namespace AdapterLib
{
    //
    // AdapterValue.
    // Description:
    // The class that implements IAdapterValue from BridgeRT.
    //
    class AdapterValue : IAdapterValue
    {
        // public properties
        public string Name { get; }
        public object Data { get; set; }

        public string Path { get; set; }

        public int SignalHashCode { get; set; }

        internal AdapterValue(string ObjectName, object DefaultData, string InnerPath = null)
        {
            this.Name = ObjectName;
            this.Data = DefaultData;
            this.Path = InnerPath;
        }

        internal AdapterValue(AdapterValue Other)
        {
            this.Name = Other.Name;
            this.Data = Other.Data;
            this.Path = Other.Path;
        }
    }

    //
    // AdapterProperty.
    // Description:
    // The class that implements IAdapterProperty from BridgeRT.
    //
    class AdapterProperty : IAdapterProperty
    {
        // public properties
        public string Name { get; }
        public string InterfaceHint { get; }
        public IList<IAdapterAttribute> Attributes { get; }

        internal AdapterProperty(string ObjectName, string IfHint)
        {
            this.Name = ObjectName;
            this.InterfaceHint = IfHint;

            try
            {
                this.Attributes = new List<IAdapterAttribute>();
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal AdapterProperty(AdapterProperty Other)
        {
            this.Name = Other.Name;
            this.InterfaceHint = Other.InterfaceHint;

            try
            {
                this.Attributes = new List<IAdapterAttribute>(Other.Attributes);
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }
    }

    //
    // AdapterAttribute.
    // Description:
    // The class that implements IAdapterAttribute from BridgeRT.
    //
    class AdapterAttribute : IAdapterAttribute
    {
        // public properties
        public IAdapterValue Value { get; }

        public E_ACCESS_TYPE Access { get; set; }
        public IDictionary<string, string> Annotations { get; }
        public SignalBehavior COVBehavior { get; set; }

        internal AdapterAttribute(string ObjectName, object DefaultData, string InnerPath = null, E_ACCESS_TYPE access = E_ACCESS_TYPE.ACCESS_READ)
        {
            try
            {
                this.Value = new AdapterValue(ObjectName, DefaultData, InnerPath);
                this.Annotations = new Dictionary<string, string>();
                this.Access = access;
                this.COVBehavior = SignalBehavior.Never;
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal AdapterAttribute(AdapterAttribute Other)
        {
            this.Value = Other.Value;
            this.Annotations = Other.Annotations;
            this.Access = Other.Access;
            this.COVBehavior = Other.COVBehavior;
        }
    }

    //
    // AdapterMethod.
    // Description:
    // The class that implements IAdapterMethod from BridgeRT.
    //
    class AdapterMethod : IAdapterMethod
    {
        // public properties
        public string Name { get; }

        public string Description { get; }

        public string Path { get; set; }

        public IList<IAdapterValue> InputParams { get; set; }

        public IList<IAdapterValue> OutputParams { get; }

        public int HResult { get; private set; }

        internal AdapterMethod(
            string ObjectName,
            string Description,
            int ReturnValue, string Path)
        {
            this.Name = ObjectName;
            this.Description = Description;
            this.HResult = ReturnValue;
            this.Path = Path;
            try
            {
                this.InputParams = new List<IAdapterValue>();
                this.OutputParams = new List<IAdapterValue>();
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal AdapterMethod(AdapterMethod Other)
        {
            this.Name = Other.Name;
            this.Description = Other.Description;
            this.HResult = Other.HResult;

            try
            {
                this.InputParams = new List<IAdapterValue>(Other.InputParams);
                this.OutputParams = new List<IAdapterValue>(Other.OutputParams);
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal void SetResult(int ReturnValue)
        {
            this.HResult = ReturnValue;
        }
    }

    //
    // AdapterSignal.
    // Description:
    // The class that implements IAdapterSignal from BridgeRT.
    //
    class AdapterSignal : IAdapterSignal
    {
        // public properties
        public string Name { get; }

        public IList<IAdapterValue> Params { get; }

        internal AdapterSignal(string ObjectName)
        {
            this.Name = ObjectName;

            try
            {
                this.Params = new List<IAdapterValue>();
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal AdapterSignal(AdapterSignal Other)
        {
            this.Name = Other.Name;

            try
            {
                this.Params = new List<IAdapterValue>(Other.Params);
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }
    }

    //
    // AdapterDevice.
    // Description:
    // The class that implements IAdapterDevice from BridgeRT.
    //
    class AdapterDevice : IAdapterDevice,
                            IAdapterDeviceLightingService,
                            IAdapterDeviceControlPanel
    {
        //private readonly string AccessToken;

        // Object Name
        public string Name { get; }

        // Device information
        public string Vendor { get; }

        public string Model { get; }

        public string Version { get; }

        public string FirmwareVersion { get; }

        public string SerialNumber { get; }

        public string Description { get; }

        // Device properties
        public IList<IAdapterProperty> Properties { get; }

        // Device methods
        public IList<IAdapterMethod> Methods { get; }

        // Device signals
        public IList<IAdapterSignal> Signals { get; }

        public IDictionary<int, IAdapterSignal> SignalsDict { get; }

        public AdapterProperty Property = new AdapterProperty("Properties", string.Empty);

        // Control Panel Handler
        public IControlPanelHandler ControlPanelHandler
        {
            get
            {
                return null;
            }
        }

        // Lighting Service Handler
        public ILSFHandler LightingServiceHandler
        {           
                get; protected set;
        }

        // Icon
        public IAdapterIcon Icon
        {
            get
            {
                return null;
            }
        }


        public AdapterDevice(
            string Name,
            string VendorName,
            string Model,
            string Version,
            string SerialNumber,
            string Description)
        {
            this.Name = Name;
            this.Vendor = VendorName;
            this.Model = Model;
            this.Version = Version;
            this.FirmwareVersion = Version;
            this.SerialNumber = SerialNumber;
            this.Description = Description;

            //this.AccessToken = accessToken;

            try
            {
                this.Properties = new List<IAdapterProperty>();
                this.Methods = new List<IAdapterMethod>();
                this.Signals = new List<IAdapterSignal>();
                this.SignalsDict = new Dictionary<int, IAdapterSignal>();
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal AdapterDevice(AdapterDevice Other)
        {
            this.Name = Other.Name;
            this.Vendor = Other.Vendor;
            this.Model = Other.Model;
            this.Version = Other.Version;
            this.FirmwareVersion = Other.FirmwareVersion;
            this.SerialNumber = Other.SerialNumber;
            this.Description = Other.Description;

            //this.AccessToken = Other.AccessToken;

            try
            {
                this.Properties = new List<IAdapterProperty>(Other.Properties);
                this.Methods = new List<IAdapterMethod>(Other.Methods);
                this.Signals = new List<IAdapterSignal>(Other.Signals);
            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        internal void AddChangeOfValueSignal(
            IAdapterProperty Property,
            IAdapterValue Attribute)
        {
            try
            {
                AdapterSignal covSignal = new AdapterSignal(Constants.CHANGE_OF_VALUE_SIGNAL);

                // Property Handle
                AdapterValue propertyHandle = new AdapterValue(
                                                    Constants.COV__PROPERTY_HANDLE,
                                                    Property);

                // Attribute Handle
                AdapterValue attrHandle = new AdapterValue(
                                                    Constants.COV__ATTRIBUTE_HANDLE,
                                                    Attribute);

                covSignal.Params.Add(propertyHandle);
                covSignal.Params.Add(attrHandle);

                this.Signals.Add(covSignal);

                try
                {
                    ((AdapterValue)Attribute).SignalHashCode = covSignal.GetHashCode();
                    this.SignalsDict.Add(covSignal.GetHashCode(), covSignal);
                    //this.SignalsDict.Add(Attribute.GetHashCode().ToString(), covSignal);
                }
                catch (ArgumentException ex)
                {
                    //ex.Message
                    System.Diagnostics.Debug.WriteLine("ex.Message.............." + ex.Message);
                }

            }
            catch (OutOfMemoryException ex)
            {
                throw;
            }
        }

        public void UpdatePropertyValue(IAdapterSignal covSignal, Adapter adapter)
        {
            //This will update attribute which has been changed
            foreach (IAdapterValue param in covSignal.Params)
            {
                if (param.Name == Constants.COV__ATTRIBUTE_HANDLE)
                {
                    adapter.NotifySignalListener(covSignal);
                }
            }

        }

        public virtual IAdapterSignal getSignal(string key, string value)
        {
            return null;
        }

        public virtual string[] getKeyValue(AdapterMethod method)
        {
            return null;
        }
    }
}
