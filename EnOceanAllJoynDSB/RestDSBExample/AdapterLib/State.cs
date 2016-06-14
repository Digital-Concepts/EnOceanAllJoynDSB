using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{
    sealed class  StreamDevices
    {
        public Header header { get; set; }
        public IList<Device> devices { get; set; }
    }

    public sealed class State {
         public string key { get; set; }
        public object value { get; set; }
        public string meaning { get; set; }
        public string timestamp { get; set; }
        public int age { get; set; }
        public string unit { get; set; }
    }
}
