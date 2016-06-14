using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{


    public sealed class Range
    {
        public double min { get; set; }
        public double max { get; set; }
        public double step { get; set; }
        public string unit { get; set; }
    }

    public sealed class Value
    {
        public string meaning { get; set; }
        public Range range { get; set; }
        public string value { get; set; }
    }

    public sealed class Function
    {
        public string key { get; set; }
        public IList<Value> values { get; set; }
        public bool transmitOnConnect { get; set; }
        public bool transmitOnEvent { get; set; }
        public bool transmitOnDuplicate { get; set; }
        public bool testExists { get; set; }
        public object defaultValue { get; set; }
        public string description
        {
            get; set;
        }
    }
    public sealed class FunctionGroup
    {
        public string title { get; set; }
        public string direction { get; set; }
        public IList<Function> functions { get; set; }
    }

    public sealed class Profile
    {
        public string eep { get; set; }
        public string title { get; set; }
        public IList<FunctionGroup> functionGroups { get; set; }
    }

    public sealed class ProfileDefination
    {
        public Header header { get; set; }
        public Profile profile { get; set; }
    }
}
