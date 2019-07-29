using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadingExamples
{

    public class FutureOptions
    {
        public bool UseThreadPool { get; set; } = false;

        internal IFutureExecution<T> CreateExecutor<T>()
        {
            if (UseThreadPool == true)
                return new ThreadPoolExecution<T>();
            else return new ThreadedExecution<T>();
        }
    }

    public interface IFutureExecution<T>
    {
        void Execute(Func<T> func);

        bool IsCompleted { get; }

        T GetResult();
    }

    public class ThreadedExecution<T> : IFutureExecution<T>
    {
        public T Result { get; private set; }
        public Exception Fault { get; private set; }
        public Thread Thread { get; private set; }
        public bool IsCompleted { get; private set; }
        public void Execute(Func<T> func)
        {
            Console.WriteLine("Running thread.");
            Thread = new Thread(_ =>
            {
                try
                {
                    Result = func();
                }
                catch (Exception ex)
                {
                    Fault = ex;
                }
                finally
                {
                    IsCompleted = true;
                }
            });
            Thread.Start();
        }

        public T GetResult()
        {
            Thread.Join();
            if (Fault != null)
                throw Fault;
            else return Result;
        }
    }

    public class ThreadPoolExecution<T> : IFutureExecution<T>
    {
        public bool IsCompleted => throw new NotImplementedException();

        public T Result { get; private set; }

        public Exception Fault { get; private set; }

        private ManualResetEvent _waiter = new ManualResetEvent(false);
        public void Execute(Func<T> func)
        {
            Console.WriteLine("Running thread.");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Result = func();
                }
                catch (Exception ex)
                {
                    Fault = ex;
                }
                finally
                {
                    _waiter.Set();
                }
            });
        }

        public T GetResult()
        {
            _waiter.WaitOne();
            if (Fault != null)
                throw Fault;
            else return Result;
        }
    }

    public class Future<T>
    {
        public Future( Func<T> func, FutureOptions options = null)
        {
            options = options ?? new FutureOptions { UseThreadPool = false };
            _execution = options.CreateExecutor<T>();
            _execution.Execute(func);
        }

        private IFutureExecution<T> _execution = null;

        public T GetResult()
        {
            return _execution.GetResult();
        }

        public Future<TOut> ContinueWith<TOut>( Func<T, TOut> continueWith)
        {

        }

    }

    public static class Extentions
    {
        public static void For<T>(this IEnumerable<T> items, Action<T> action)
        {
            foreach (var item in items)
                action(item);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var worker = new Worker();
            ConcurrentBag<string> results = new ConcurrentBag<string>();
            //var threads = new List<Thread>();
            Enumerable.Range(1, 10)
                      .Select(t => new Thread(_ =>
                    {
                        results.Add(worker.Run(1000));
                    }))
                      .ToList()
                      .Select(t =>
                     {
                         t.Start();
                         return t;
                     })
                      .For(t => t.Join());

            results.For(x => Console.WriteLine(x));

            Console.WriteLine("Starting");
            Enumerable
                    .Range(1, 10)
                    .Select(_ => new Future<string>(() => worker.Run(1000)))
                    .ToList()
                    .Select(w => w.GetResult())
                    .For( x => Console.WriteLine(x));
            // var opt = new ParallelOptions { MaxDegreeOfParallelism = 20 };
            // Parallel.For(0, 10, opt, _ => worker.Run());
            /*
            Enumerable.Range(1, 10)
                      .Select(_ => new Thread(worker.Run))
                      .Select(t =>
                     {
                         t.Start();
                         return t;
                     })
                      .ToList()
                      .ForEach(t => t.Join());

            for (int i = 0; i < 10; i++)
            {
                var thread = new Thread(_ => worker.Run());
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
                thread.Join();
                */
            Console.WriteLine("Done");
            Console.ReadKey(true);
        }
    }

    public class Worker
    {
        public string Id { get; } = Path.GetRandomFileName();

        public string Run(int sleepTime)
        {
            Thread.Sleep(sleepTime);
            return $"[Id] Did some work on thread {Thread.CurrentThread.ManagedThreadId}";
        }
    }
}
