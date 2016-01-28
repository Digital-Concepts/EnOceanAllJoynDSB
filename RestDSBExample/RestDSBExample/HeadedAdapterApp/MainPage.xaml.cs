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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace HeadedAdapterApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void start_Click(object sender, RoutedEventArgs e)
        {
            string DCGW_URL = DCGWURL.Text;

            //This code was taken from App.xaml.cs 
            //This code is placed here becuase we need to start Bridge after IP of EnOcean Gateway is available
            await ThreadPool.RunAsync(new WorkItemHandler((IAsyncAction action) =>
              {
                  try
                  {
                      var adapter = new AdapterLib.Adapter(DCGW_URL);
                      DsbBridge dsbBridge = new DsbBridge(adapter);

                      //Change made by zahoor (25/1/2016)
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

            start.Visibility = Visibility.Collapsed;
        }

    }
}
