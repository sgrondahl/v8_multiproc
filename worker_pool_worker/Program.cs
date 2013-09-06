using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using SubProcIPC;
using Noesis.Javascript;

namespace worker_pool_worker {
    class Program {
        private static JavascriptContext JSContext = new JavascriptContext();
        private static ManualResetEvent quitEvent = new ManualResetEvent(false);

        static void Main(string[] args) {
            SubProcessClient sp = new SubProcessClient(args, AsyncHandleServerRequest);

            Console.WriteLine("Client: setting up javascript");

            JSContext.SetParameter("console", Console.Out);
            JSContext.SetParameter("sp_request", sp);

            string filename = "Scripts.js";
            string filecontent = File.ReadAllText(filename);
            JSContext.Run(filecontent, filename);
            
            Console.WriteLine("Client: starting");
            sp.Start();
            quitEvent.WaitOne();
            Console.WriteLine("Client: quitting");
            JSContext.Dispose();
        }

        static void AsyncHandleServerRequest(SubProcess.RequestHandle request, string message) {
            RequestHandler rc = HandleServerRequest;
            rc.BeginInvoke(request, message, null, null);
        }

        static void HandleServerRequest(SubProcess.RequestHandle request, string message) {
            string[] stuff = message.Split(new char[] { ';' }, 2);
            switch (stuff[0]) {
                case "js":
                    try {
                        object o = JSContext.Run(stuff[1], "*input*");
                        request.Respond("success;" + o.ToString());
                    } catch (JavascriptException x) {
                        request.Respond("failure;" + (string)x.Data["V8StackTrace"]);
                    }
                    break;
                case "name":
                    request.Respond("bob");
                    break;
                case "quit":
                    request.Respond("quitting");
                    quitEvent.Set();
                    break;
                case "blarble":
                    request.Respond(request.SubProc.Request("this is a request"));
                    break;
                default:
                    request.Respond("error");
                    break;
            }
        }
    }
}
