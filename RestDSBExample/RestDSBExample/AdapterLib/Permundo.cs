using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{
    class Permundo : AdapterDevice
    {
        public Permundo(string Name, string VendorName, string Model, string Version, string SerialNumber, string Description) : base(Name,
            VendorName, Model, Version, SerialNumber, Description)
        {
            base.LightingServiceHandler = new LightingServiceHandler(this);
        }
        
        public void turnOnOff(bool LampState_OnOff) {

            UInt16 LampState = 0;
            if (LampState_OnOff)
                LampState = 100;

            setDimValue(LampState);
        }

        public void updateStates(UInt16 LampState_OnOff) {

            bool permundoState = false;
            if (LampState_OnOff > 0)
                permundoState = true;

            base.LightingServiceHandler.LampState_OnOff = permundoState;
            base.LightingServiceHandler.LampState_Brightness = LampState_OnOff;
        }        

        public Adapter adapter
        {
            get; set;
        }

        internal void dim(UInt16 _LampState_Brightness)
        {
            setDimValue(_LampState_Brightness);
        }


        void setDimValue(UInt16 value)
        {
            object valueData = Windows.Foundation.PropertyValue.CreateUInt16(value);
            string path = "devices/" + base.SerialNumber + "/state";
            adapter.SetHttpValue(path, valueData, "dimValue");
        }
    }
}
