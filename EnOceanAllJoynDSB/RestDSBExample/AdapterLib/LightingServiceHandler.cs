using BridgeRT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{
    internal class LightingServiceHandler : ILSFHandler
    {
        public LightingServiceHandler(Lamp lamp) {
            this.lamp = lamp;
            
            //LampDetails_Color = false;           
            LampDetails_Dimmable = true;
            //LampState_OnOff = true;
            LampDetails_HasEffects = false;
            //LampDetails_IncandescentEquivalent = 60;
            //LampDetails_LampBaseType = (uint)1;
            //LampDetails_LampBeamAngle = 130;
            LampDetails_LampID = this.lamp.SerialNumber;
            //LampDetails_LampType = (uint)1;
            //LampDetails_Make = (uint)AdapterLib.LsfEnums.LampMake.MAKE_OEM1;
            //LampDetails_MaxLumens = 100;
            //LampDetails_MaxTemperature = 100;
            //LampDetails_MaxVoltage = 100;
            //LampDetails_MinTemperature = 0;
            //LampDetails_MinVoltage = 100;
            //LampDetails_Model = 1;
            //LampDetails_Type = (uint)AdapterLib.LsfEnums.DeviceType.TYPE_LAMP;
            //LampDetails_VariableColorTemp = false;
            //LampDetails_Version = 1;
            //LampDetails_Wattage = 0;
            //if(!LampDetails_Color) LampState_Saturation = 0;
            LampState_Version = 101;
        }
  
        private Lamp lamp
        {
            get; set;
        }

        public bool LampDetails_Color{ get; private set; }

        public uint LampDetails_ColorRenderingIndex { get; private set; }

        public bool LampDetails_Dimmable { get; private set; }

        public bool LampDetails_HasEffects { get; private set; }

        public uint LampDetails_IncandescentEquivalent { get; private set; }

        public uint LampDetails_LampBaseType { get; private set; }

        public uint LampDetails_LampBeamAngle { get; private set; }

        public string LampDetails_LampID { get; private set; }

        public uint LampDetails_LampType { get; private set; }

        public uint LampDetails_Make { get; private set; }

        public uint LampDetails_MaxLumens { get; private set; }

        public uint LampDetails_MaxTemperature { get; private set; }

        public uint LampDetails_MaxVoltage { get; private set; }

        public uint LampDetails_MinTemperature { get; private set; }

        public uint LampDetails_MinVoltage { get; private set; }

        public uint LampDetails_Model { get; private set; }

        public uint LampDetails_Type { get; private set; }

        public bool LampDetails_VariableColorTemp { get; private set; }

        public uint LampDetails_Version { get; private set; }

        public uint LampDetails_Wattage { get; private set; }

        public uint LampParameters_BrightnessLumens {
            get
            {
                if (!LampState_OnOff)
                    return 0;
                return 100;
            }
        }

        public uint LampParameters_EnergyUsageMilliwatts {
            get
            {
                if (!LampState_OnOff)
                    return 0;
                return 100;
            }
        }

        public uint LampParameters_Version { get; private set; }

        
        public uint[] LampService_LampFaults { get; private set; }

        public uint LampService_LampServiceVersion { get; private set; }

        public uint LampService_Version { get; private set; }
                                   
        
        public uint LampState_ColorTemp { get; set; }
        
        public uint LampState_Hue { get;  set; }



        private AdapterSignal _LampStateChanged = new AdapterSignal(Constants.LAMP_STATE_CHANGED_SIGNAL_NAME);

        public IAdapterSignal LampState_LampStateChanged {
            get
            {
                return _LampStateChanged;
            }
        }


        private bool _LampState_OnOff;
        public bool LampState_OnOff {
            get {
                return _LampState_OnOff;
            }

            set {
                    _LampState_OnOff = value;
                    lamp.turnOnOff(_LampState_OnOff);
            }

        }

        public uint LampState_Saturation { get; set; }


        public uint LampState_Version { get; private set; }

        private uint _LampState_Brightness;
        public uint LampState_Brightness {
            get { return _LampState_Brightness; }
            set
            {
                if (LampDetails_Dimmable)
                {
                    _LampState_Brightness = value;
                    lamp.dim((UInt16)_LampState_Brightness);
                }
            }
        }            
        
        public uint LampState_ApplyPulseEffect(State FromState, State ToState, uint Period, uint Duration, uint NumPulses, ulong Timestamp, out uint LampResponseCode)
        {
            LampResponseCode = 0;
            return 0; //TODO
        }

        public uint TransitionLampState(ulong Timestamp, State NewState, uint TransitionPeriod, out uint LampResponseCode)
        {
            LampResponseCode = 0;
            return 0; //TODO
        }           

        uint ILSFHandler.ClearLampFault(uint InLampFaultCode, out uint LampResponseCode, out uint OutLampFaultCode)
        {
            throw new NotImplementedException();
        }

        public uint ClearLampFault(uint InLampFaultCode, out uint LampResponseCode, out uint OutLampFaultCode)
        {
            InLampFaultCode = 0;
            LampResponseCode = 0;
            OutLampFaultCode = 0;
            return 0; //TODO
        }

        public uint TransitionLampState(ulong Timestamp, BridgeRT.State NewState, uint TransitionPeriod, out uint LampResponseCode)
        {
            throw new NotImplementedException();
        }

        public uint LampState_ApplyPulseEffect(BridgeRT.State FromState, BridgeRT.State ToState, uint Period, uint Duration, uint NumPulses, ulong Timestamp, out uint LampResponseCode)
        {
            throw new NotImplementedException();
        }
    }
}
