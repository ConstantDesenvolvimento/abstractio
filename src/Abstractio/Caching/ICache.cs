namespace Abstractio.Caching
{
    public interface ICache
    {
        object Get(string key);
        void Set(string key, object o);
        void Remove(string key);
    }
}