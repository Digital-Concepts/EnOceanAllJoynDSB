using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using BridgeRT;
using AdapterLib;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HeadedAdapterApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        DsbBridge dsbBridge = null;
        Adapter adapter = null;
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void start_Click(object sender, RoutedEventArgs e)
        {
            //string DCGW_URL = DCGWURL.Text;
            //string user = userName.Text;
            //string password = userPassword.Text;

            //This code is placed here becuase we need to start Bridge after IP of EnOcean Gateway is available
            await ThreadPool.RunAsync(new WorkItemHandler((IAsyncAction action) =>
              {
                  try
                  {
                      //adapter = new AdapterLib.Adapter(DCGW_URL, user, pas);
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
                      throw ex;
                  }
              }));

            //start.Visibility = Visibility.Collapsed;
            //stop.Visibility = Visibility.Visible;
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {           
            if (dsbBridge != null)
            {
                //dsbBridge?.Shutdown();
                dsbBridge.Shutdown();
            }

            if (adapter != null)
            {
                adapter.Shutdown();
            }

            //stop.Visibility = Visibility.Collapsed;
            //start.Visibility = Visibility.Visible;
        }
    }
}
