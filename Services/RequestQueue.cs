using DrugiProjekat.Models;
using DrugiProjekat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrugiProjekat.Services
{
    public class RequestQueue
    {
        private readonly Queue<ClientRequest> _queue = new Queue<ClientRequest>();
        private readonly object _queueLock = new object();
        private bool _isRunning = true;

        private const int MaxQueueSize = 100;

        public bool Enqueue(ClientRequest request)
        {
            lock (_queueLock)
            {
                if (_queue.Count >= MaxQueueSize)
                {
                    Logger.Warn($"Red je pun ({MaxQueueSize}), zahtev {request.RequestId} odbijen!");
                    return false;
                }

                _queue.Enqueue(request);
                Logger.Info($"Zahtev {request.RequestId} dodat u red. Velicina: {_queue.Count}");

                Monitor.Pulse(_queueLock);
                return true;
            }
        }

        public ClientRequest? Dequeue()
        {
            lock (_queueLock)
            {
                while (_queue.Count == 0 && _isRunning)
                {
                    Logger.Info($"Radna nit ceka na zahtev...");
                    Monitor.Wait(_queueLock);
                }

                if (!_isRunning && _queue.Count == 0)
                    return null;

                var request = _queue.Dequeue();
                Logger.Info($"Zahtev {request.RequestId} preuzet iz reda. Preostalo: {_queue.Count}");
                return request;
            }
        }

        public void Shutdown()
        {
            lock (_queueLock)
            {
                _isRunning = false;
                Monitor.PulseAll(_queueLock);
                Logger.Info("Red zahteva je ugasen.");
            }
        }
    }
}
