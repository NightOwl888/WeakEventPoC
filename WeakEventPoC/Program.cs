using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WeakEventPoC
{
    public class Program
    {
        static void Main(string[] args)
        {
            var manager = new CacheManager();

            var key1 = new MyKey("key1");
            manager.Add(key1);

            var key2 = new MyKey("key2");
            manager.Add(key2);

            Console.WriteLine($"RAMUsage: {manager.GetRAMUsage()}");

            key1.Dispose();
            //manager.Remove(key1); // The user *should* remove the item, but if they don't it will still function as long as Dispose() is called
            key1 = null;

            Console.WriteLine($"RAMUsage: {manager.GetRAMUsage()}");


            Console.WriteLine("Hello World!");
        }
    }

    public class CacheManager
    {
        private readonly static ConditionalWeakTable<MyKey, object> cache = new ConditionalWeakTable<MyKey, object>();
        public event EventHandler<NotifyRAMEventArgs> NotifyRAMEvent;

        public void Add(MyKey key)
        {
            key.manager = this;
            NotifyRAMEventWeakEventManager.AddHandler(this, key.OnNotifyRAM);
            cache.Add(key, new object());
        }

        public void Remove(MyKey key)
        {
            cache.Remove(key);
            NotifyRAMEventWeakEventManager.RemoveHandler(this, key.OnNotifyRAM);
        }

        public long GetRAMUsage()
        {
            var args = new NotifyRAMEventArgs();
            NotifyRAMEvent?.Invoke(this, args);
            return args.RAMBytesUsed;
        }
    }

    public class MyKey : IDisposable
    {
        private readonly string name;
        internal CacheManager manager;
        public MyKey(string name)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));
        }

        ~MyKey()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Console.WriteLine($"{name} disposed");
            else
                Console.WriteLine($"{name} finalized");

            NotifyRAMEventWeakEventManager.RemoveHandler(manager, OnNotifyRAM);
        }

        internal void OnNotifyRAM(object source, NotifyRAMEventArgs e)
        {
            e.RAMBytesUsed += 3;
        }
    }

    public class NotifyRAMEventArgs : EventArgs
    {
        public long RAMBytesUsed { get; set; }
    }

    public class NotifyRAMEventWeakEventManager : WeakEventManager
    {
        private NotifyRAMEventWeakEventManager() { }

        /// <summary>
        /// Add a handler for the given source's event.
        /// </summary>
        public static void AddHandler(CacheManager source,
                                      EventHandler<NotifyRAMEventArgs> handler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            CurrentManager.ProtectedAddHandler(source, handler);
        }

        /// <summary>
        /// Remove a handler for the given source's event.
        /// </summary>
        public static void RemoveHandler(CacheManager source,
                                         EventHandler<NotifyRAMEventArgs> handler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            CurrentManager.ProtectedRemoveHandler(source, handler);
        }

        /// <summary>
        /// Get the event manager for the current thread.
        /// </summary>
        private static NotifyRAMEventWeakEventManager CurrentManager
        {
            get
            {
                Type managerType = typeof(NotifyRAMEventWeakEventManager);
                NotifyRAMEventWeakEventManager manager =
                    (NotifyRAMEventWeakEventManager)GetCurrentManager(managerType);

                // at first use, create and register a new manager
                if (manager == null)
                {
                    manager = new NotifyRAMEventWeakEventManager();
                    SetCurrentManager(managerType, manager);
                }

                return manager;
            }
        }

        /// <summary>
        /// Return a new list to hold listeners to the event.
        /// </summary>
        protected override ListenerList NewListenerList()
        {
            return new ListenerList<NotifyRAMEventArgs>();
        }

        protected override void StartListening(object source)
        {
            CacheManager typedSource = (CacheManager)source;
            typedSource.NotifyRAMEvent += new EventHandler<NotifyRAMEventArgs>(OnNotifyRAM);
        }

        protected override void StopListening(object source)
        {
            CacheManager typedSource = (CacheManager)source;
            typedSource.NotifyRAMEvent -= new EventHandler<NotifyRAMEventArgs>(OnNotifyRAM);
        }

        void OnNotifyRAM(object sender, NotifyRAMEventArgs e)
        {
            DeliverEvent(sender, e);
        }
    }
}
