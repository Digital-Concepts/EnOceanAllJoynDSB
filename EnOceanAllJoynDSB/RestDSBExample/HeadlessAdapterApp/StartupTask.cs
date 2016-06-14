using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using BridgeRT;
using AdapterLib;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace HeadlessAdapterApp
{
    public sealed class StartupTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {

            Adapter adapter = null;
            deferral = taskInstance.GetDeferral();

            try
            {
                //dcgw.enocean-gateway.eu is IP of EnOcean gateway, it needs to be change if you are using local or different gateway
                //Using this IP of gateway, you can see changes in gateway and devices on "http://dcgw.enocean-gateway.eu:8080/devices/stream" (Streaming of gateway)
                adapter = new Adapter("", "", "");
                adapter.setDCGWAttributes();
                dsbBridge = new DsbBridge(adapter);

                //Adapter object need dsbBridge to Add new devices
                adapter.setDsbBridge(dsbBridge);

                var initResult = dsbBridge.Initialize();
                if (initResult != 0)
                {
                    throw new Exception("DSB Bridge initialization failed!");
                }
            }
            catch (Exception ex)
            {
                if (dsbBridge != null)
                {
                    dsbBridge.Shutdown();
                }

                if (adapter != null)
                {
                    adapter.Shutdown();
                }

                throw;
            }
        }

        private DsbBridge dsbBridge;
        private BackgroundTaskDeferral deferral;
    }
}
