using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace BlueArchiveAPI.NetworkModels
{
    public interface IPacket
    {
        Protocol Protocol { get; }
    }

    public interface IRequest : IPacket
    {
    }

    public interface IResponse : IPacket
    {
    }
    public interface IRequest<T> : IRequest where T : IResponse
    {
    }

    public interface IResponse<T> : IResponse where T : IRequest
    {
    }

    public class TypedJsonWrapper<T> where T : class
    {
        public T Instance;

        public static implicit operator TypedJsonWrapper<T>(T obj)
        {
            return new TypedJsonWrapper<T>(obj);
        }

        public TypedJsonWrapper(T obj)
        {
            Instance = obj;
        }
    }

    public struct Vector3
    {
        public float x, y, z;
    }
}
