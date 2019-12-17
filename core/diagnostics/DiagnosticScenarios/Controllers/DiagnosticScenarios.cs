using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace testwebapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagScenarioController : ControllerBase
    {
        private object _o1 = new object();
        private object _o2 = new object();

        private static Processor p = new Processor();

        [HttpGet]
        [Route("deadlock/")]
        public ActionResult<string> deadlock()
        {
            (new System.Threading.Thread(() => {
                DeadlockFunc();
            })).Start();

            Thread.Sleep(5000);

            var threads = new Thread[300];
            for (int i = 0; i < 300; i++)
            {
                (threads[i] = new Thread(() => {
                    lock (_o1) {Thread.Sleep(100);}
                })).Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            return "success:deadlock";
        }

        private void DeadlockFunc()
        {
            lock (_o1)
            {
                (new Thread(() => {
                    lock (_o2) { Monitor.Enter(_o1); }
                })).Start();

                Thread.Sleep(2000);
                Monitor.Enter(_o2);
            }
        }

        [HttpGet]
        [Route("memspike/{seconds}")]
        public ActionResult<string> memspike(int seconds)
        {
            var watch = new Stopwatch();
            watch.Start();

            while (true)
            {
                p = new Processor();
                watch.Stop();
                if (watch.ElapsedMilliseconds > seconds * 1000)
                    break;
                watch.Start();

                int it = (2000 * 1000);
                for (int i = 0; i < it; i++)
                {
                    p.ProcessTransaction(new Customer(Guid.NewGuid().ToString()));
                }

                Thread.Sleep(5000);	// Sleep for 5 seconds before cleaning up

                // Cleanup
                p = null;

                // GC
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Thread.Sleep(5000);	// Sleep for 5 seconds before spiking memory again
            }
            return "success:memspike";
        }

        [HttpGet]
        [Route("memleak/{kb}")]
        public ActionResult<string> memleak(int kb)
        {
            int it = (kb * 1000) / 100;
            for (int i = 0; i < it; i++)
            {
                p.ProcessTransaction(new Customer(Guid.NewGuid().ToString()));
            }

            return "success:memleak";
        }

        [HttpGet]
        [Route("exception")]
        public ActionResult<string> exception()
        {
            throw new Exception("bad, bad code");
        }


        [HttpGet]
        [Route("highcpu/{milliseconds}")]
        public ActionResult<string> highcpu(int milliseconds)
        {
            var watch = new Stopwatch();
            watch.Start();

            while (true)
            {
                 watch.Stop();
                 if (watch.ElapsedMilliseconds > milliseconds)
                     break;
                 watch.Start();
            }

            return "success:highcpu";
        }
    }

    class Customer
    {
        private string _id;

        public Customer(string id)
        {
            _id = id;
        }
    }

    class CustomerCache
    {
        private List<Customer> _cache = new List<Customer>();

        public void AddCustomer(Customer c)
        {
            _cache.Add(c);
        }
    }

    class Processor
    {
        private CustomerCache _cache = new CustomerCache();

        public void ProcessTransaction(Customer customer)
        {
            _cache.AddCustomer(customer);
        }
    }
}
