using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using SubProcIPC;

namespace _9_6_worker_pool_test {
    class Program {
        static void Main(string[] args) {
            SubProcessServer sp = new SubProcessServer("worker_pool_worker.exe", HandleClientRequest);
            Console.WriteLine("Server: starting");
            sp.Start();

            Console.WriteLine("Server: got client name: {0}", sp.Request("name"));
            Console.WriteLine("Server: got 'js;1+1' = {0}", sp.Request("js;1+1"));
            Console.WriteLine("Server: got 'js;blaz()' = {0}", sp.Request("js;blaz()"));
            Console.WriteLine("Server: got 'blarble' = {0}", sp.Request("blarble"));
            Console.WriteLine("Server: got 'js;blarble()' = {0}", sp.Request("js;blarble()"));
            Console.WriteLine("Server: got 'js;err()' = {0}", sp.Request("js;err()"));

            Console.WriteLine("Server: asking client to quit");
            sp.Request("quit");
            sp.WaitForExit();
            Console.WriteLine("Server: client quit");
        }

        static void HandleClientRequest(SubProcess.RequestHandle request, string message) {
            Console.WriteLine("Server: got client request: {0}", message);
            request.Respond("Got it (" + message + ").");
        }

        static void TestSpeed(SubProcessServer sp) {
            Stopwatch sw = new Stopwatch();
            int numToTry = 100000;
            string name = null;
            sw.Start();
            for (int i = 0; i < numToTry; i++) {
                name = sp.Request("name");
            }
            sw.Stop();
            Console.WriteLine("Server: got name: {0}", name);
            Console.WriteLine("Server: did {0} requests per second", numToTry / sw.Elapsed.TotalSeconds);
        }
    }
}
