using System;
using System.Text.Json.Serialization;

namespace Schale.MX.Logic.Battles
{
    public enum BattleEndType
    {
        None = 0,
        AllNearlyDead = 1,
        TimeOut = 2,
        EscortFailed = 3,
        Clear = 4,
    }

	[JsonConverter(typeof(JsonStringEnumConverter))]
    [Flags]
	public enum GroupTag
	{
		None = 0,
		Group01 = 1,
		Group02 = 2,
		Group03 = 4,
		Group04 = 8,
		Group05 = 16,
		Group06 = 32,
		Group07 = 64,
		Group08 = 128,
		Group09 = 256,
		Group10 = 512,
		Group11 = 1024,
		Group12 = 2048,
		Group13 = 4096,
		Group14 = 8192,
		Group15 = 16384,
		Group16 = 32768
	}
}




