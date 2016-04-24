using System.Collections.Generic;

namespace Abstractio.Caching
{
    public class MemoryCache : ICache
    {
        private readonly Dictionary<string, object> _container = new Dictionary<string, object>();

        public object Get(string key)
        {
            object retval = null;
            _container.TryGetValue(key, out retval);
            return retval;
        }

        public void Set(string key, object o)
        {
            if (_container.ContainsKey(key))
            {
                _container[key] = o;
            }
            else
            {
                _container.Add(key, o);
            }
        }

        public void Remove(string key)
        {
            if (_container.ContainsKey(key))
            {
                _container.Remove(key);
            }
        }
    }
}