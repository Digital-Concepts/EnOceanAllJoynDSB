using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdapterLib
{

    public sealed class Telegram
    {
        public string deviceId { get; set; }
        public string friendlyId { get; set; }
        public string physicalDevice { get; set; }
        public string timestamp { get; set; }
        public string direction { get; set; }
        public IList<TelegramFunction> functions { get; set; }
        public TelegramInfo telegramInfo { get; set; }
    }

    public sealed class TelegramInfo
    {
        public string data { get; set; }
        public string status { get; set; }
        public int dbm { get; set; }
        public string rorg { get; set; }
    }

    public sealed class TelegramFunction
    {
        public string key { get; set; }
        public string value { get; set; }
        public string unit { get; set; }
    }

    public sealed class TelegramBody
    {
        public Header header { get; set; }
        public Telegram telegram { get; set; }
    }

    public sealed class Telegrams
    {
        public IList<TelegramBody> telegrams { get; set; }
    }
}
