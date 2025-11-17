using System.Text.Json.Serialization;

namespace Schale.MX.Core.Services
{
    public class TypedJsonWrapper<T> where T : class
	{
		public string JsonWithType { get; set; } = string.Empty;

		[JsonIgnore]
		public T? Instance { get => _instance; }

		public static implicit operator TypedJsonWrapper<T>(T obj)
		{
			return new TypedJsonWrapper<T>();
		}

		[JsonIgnore]
		private T? _instance;
	}
}



