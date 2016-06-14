using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{
    public sealed class Devices
    {
        public Header header { get; set; }
        public IList<Device> devices { get; set; }
    }

    //public sealed class DeviceInfo
    //{
    //    public string deviceId { get; set; }
    //    public string friendlyId { get; set; }
    //    public string physicalDevice { get; set; }
    //}


    public sealed class Device
    {
        public string deviceId { get; set; }
        public string friendlyId { get; set; }
        public string learnInProcedure { get; set; }
        public IList<ProfileInfo> eeps { get; set; }
        public string firstSeen { get; set; }
        public string lastSeen { get; set; }
        public bool secured { get; set; }
        public bool softSmartAck { get; set; }
        public IList<TransmitMode> transmitModes { get; set; }
        public IList<State> states { get; set; }
        public bool operable { get; set; }
        public bool supported { get; set; }
        public string manufacturer { get; set; }
        public string physicalDevice { get; set; }
        public bool deleted { get; set; }
    }


    public sealed class TransmitMode
    {
        public string key { get; set; }
        public bool transmitOnConnect { get; set; }
        public bool transmitOnEvent { get; set; }
        public bool transmitOnDuplicate { get; set; }
    }

    public sealed class ProfileInfo
    {
        public string eep { get; set; }
        public double version { get; set; }
        public string direction { get; set; }
        public string variation { get; set; }
    }
    public sealed class Header
    {
        public int httpStatus { get; set; }
        public string content { get; set; }
        public string gateway { get; set; }
        public string timestamp { get; set; }
    }
}

