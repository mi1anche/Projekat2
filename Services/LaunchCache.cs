using DrugiProjekat.Models;
using DrugiProjekat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrugiProjekat.Services
{
    public class LaunchCache
    {
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();

        private readonly LinkedList<string> _insertionOrder = new LinkedList<string>();

        private readonly int _maxSize;
        private readonly object _cacheLock = new object();

        private int _hits = 0;
        private int _misses = 0;
        private int _evictions = 0;

        public LaunchCache(int maxSize = 10)
        {
            _maxSize = maxSize;
            Logger.Cache($"Inicijalizovan sa max velicinom: {_maxSize} unosa");
        }

        public List<WeatherResult>? TryGet(string key)
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(key, out CacheEntry? entry))
                    return null;

                while (entry.IsLoading)
                {
                    Logger.Cache($"Nit ceka na ucitavanje kljuca '{key}' (cache stampede zastita)");
                    Monitor.Wait(_cacheLock);

                    if (!_cache.TryGetValue(key, out entry!))
                        return null;
                }

                Interlocked.Increment(ref _hits);
                Logger.Cache($"HIT za '{key}' | velicina={_cache.Count}/{_maxSize} | hits={_hits} misses={_misses}");
                return entry.Results;
            }
        }

        public void ReservePlaceholder(string key)
        {
            lock (_cacheLock)
            {
                if (_cache.ContainsKey(key))
                    return;

                if (_cache.Count >= _maxSize)
                    EvictOldest();

                _cache[key] = new CacheEntry();
                _insertionOrder.AddLast(key);

                Interlocked.Increment(ref _misses);
                Logger.Cache($"MISS za '{key}', placeholder rezervisan | velicina={_cache.Count}/{_maxSize}");
            }
        }

        public void Set(string key, List<WeatherResult> results)
        {
            lock (_cacheLock)
            {
                _cache[key] = new CacheEntry(results);

                Logger.Cache($"Sacuvano {results.Count} solova za '{key}' | velicina={_cache.Count}/{_maxSize}");
                Monitor.PulseAll(_cacheLock);
            }
        }

        public void RemovePlaceholder(string key)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(key, out var entry) && entry.IsLoading)
                {
                    _cache.Remove(key);
                    _insertionOrder.Remove(key);
                    Logger.Cache($"Placeholder uklonjen za '{key}' zbog greske.");
                }
                Monitor.PulseAll(_cacheLock);
            }
        }

        private void EvictOldest()
        {
            var node = _insertionOrder.First;
            while (node != null)
            {
                string candidate = node.Value;
                if (_cache.TryGetValue(candidate, out var e) && !e.IsLoading)
                {
                    _insertionOrder.Remove(node);
                    _cache.Remove(candidate);
                    Interlocked.Increment(ref _evictions);
                    Logger.Cache($"EVICTION (FIFO): uklonjen '{candidate}' | ukupno evictions={_evictions}");
                    return;
                }
                node = node.Next;
            }

            Logger.Warn("Eviction nije moguca - svi unosi su u stanju ucitavanja.");
        }

        public void PrintStats()
        {
            lock (_cacheLock)
            {
                Logger.Cache($"Statistike -> velicina: {_cache.Count}/{_maxSize} | hits: {_hits} | misses: {_misses} | evictions: {_evictions}");
                Logger.Cache("Redosled unosa (najstariji -> najnoviji):");
                foreach (var key in _insertionOrder)
                {
                    bool loading = _cache.TryGetValue(key, out var e) && e.IsLoading;
                    Logger.Cache($"  -> '{key}'{(loading ? " [ucitava se...]" : "")}");
                }
            }
        }
    }
}
