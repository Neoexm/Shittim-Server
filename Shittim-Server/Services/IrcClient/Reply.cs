namespace Shittim.Services.IrcClient
{
    public class Reply : EventArgs
    {
        public string Prefix { get; set; }

        public string Command { get; set; } = string.Empty;

        public ReplyCode ReplyCode { get; set; }

        public List<string> Params { get; set; }

        public string Trailing { get; set; }

        public override string ToString()
        {
            string cmd = Command == string.Empty ? $"{(int)ReplyCode:D3}" : Command;

            return $":{Prefix} {cmd} {Params} :{Trailing}";
        }
    }
}
