using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{
    class Lamp : AdapterDevice
    {
        public Lamp(string Name, string VendorName, string Model, string Version, string SerialNumber, string Description) : base(Name,
            VendorName, Model, Version, SerialNumber, Description)
        {
            base.LightingServiceHandler = new LightingServiceHandler(this);
            //base.Signals.Add(base.LightingServiceHandler.LampState_LampStateChanged);
        }
        
        public void turnOnOff(bool LampState_OnOff) {

            UInt16 LampState = 0;
            if (LampState_OnOff)
                LampState = 100;

            if (!StateUpdated) {
                setDimValue(LampState);
            }
            
        }

        public UInt16 OnOff_Value_Save { get; set; }
        public bool StateUpdated { get; set; }

        public void updateStates(UInt16 OnOff_Value) {

            bool LampState_On = false;
            if (OnOff_Value > 0)
                LampState_On = true;

            if (base.LightingServiceHandler.LampState_OnOff != LampState_On) {
                StateUpdated = true;
                base.LightingServiceHandler.LampState_OnOff = LampState_On;                
            }
            
            OnOff_Value_Save = OnOff_Value;
            base.LightingServiceHandler.LampState_Brightness = OnOff_Value;

            StateUpdated = false;
        }        

        public Adapter Adapter
        {
            get; set;
        }

        internal void dim(UInt16 _LampState_Brightness)
        {
            if (_LampState_Brightness != OnOff_Value_Save) {
                setDimValue(_LampState_Brightness);
            }            
        }


        void setDimValue(UInt16 value)
        {
            object valueData = Windows.Foundation.PropertyValue.CreateUInt16(value);
            string path = "devices/" + base.SerialNumber + "/state";
            Adapter.SetHttpValue(path, valueData, "dimValue");
            //Adapter.NotifySignalListener(base.LightingServiceHandler.LampState_LampStateChanged);

        }
    }
}
