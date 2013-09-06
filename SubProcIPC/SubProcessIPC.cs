using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;

namespace SubProcIPC
{
    public delegate void RequestHandler(SubProcess.RequestHandle request, string message);
    public delegate void RequestCallback(string message);
    
    public abstract class SubProcess : IDisposable {
        private const byte MESSAGE_TYPE_REQUEST = 1;
        private const byte MESSAGE_TYPE_RESPONSE = 2;

        private RequestHandler Handler;
        private BinaryReader FromExternal;
        private BinaryWriter ToExternal;

        private Thread ReadingThread;
        private Thread CallbackThread;
        private UInt32 NextSequenceNumber = 1;
        private Dictionary<UInt32, RequestCallback> OutstandingRequests = new Dictionary<UInt32, RequestCallback>();
        public static TimeSpan ReceiveTimeout = new TimeSpan(10000000); // 1 second
        private bool HasStarted = false;

        public SubProcess(RequestHandler handler) {
            this.Handler = handler;
        }

        protected void Initialize(Stream streamFromExternal, Stream streamToExternal) {
            ToExternal = new BinaryWriter(streamToExternal);
            FromExternal = new BinaryReader(streamFromExternal);
        }

        virtual public void Start() {
            if (HasStarted) {
                throw new InvalidOperationException("This subprocess has already been started in the past. Subprocesses cannot be restarted.");
            }
            HasStarted = true;
            
            ReadingThread = new Thread(HandleCommunication);
            ReadingThread.IsBackground = true;
            ReadingThread.Start();
        }
                
        private void HandleCommunication() {
            try {
                while (true) {
                    byte messageType = FromExternal.ReadByte();
                    UInt32 seqNum = FromExternal.ReadUInt32();
                    string message = FromExternal.ReadString();
                    if (messageType == MESSAGE_TYPE_REQUEST) {
                        Handler(new RequestHandle(this, seqNum), message);
                    } else if (messageType == MESSAGE_TYPE_RESPONSE) {
                        RequestCallback h;
                        lock (this) {
                            h = OutstandingRequests[seqNum];
                            OutstandingRequests.Remove(seqNum);
                        }
                        if (h != null) {
                            h(message);
                        }
                    } else {
                        throw new ArgumentException("Unknown message type: " + (int)messageType);
                    }
                }
            } catch (EndOfStreamException) {
                Dispose();
            }
        }

        private void PrimitiveSend(byte messageType, UInt32 seqNum, string message) {
            lock (this) {
                ToExternal.Write(messageType);
                ToExternal.Write(seqNum);
                ToExternal.Write(message);
                ToExternal.Flush();
            }
        }

        public void AsyncRequest(string message, RequestCallback callback) {
            // callback can be null
            UInt32 seqNum = NextSequenceNumber++;
            lock (this) {
                OutstandingRequests[seqNum] = callback;
            }
            PrimitiveSend(MESSAGE_TYPE_REQUEST, seqNum, message);
        }

        public string Request(string message) {
            ManualResetEvent mEvent = new ManualResetEvent(false);
            String result = null;
            AsyncRequest(message, delegate(string response) {
                result = response;
                mEvent.Set();
            });
            mEvent.WaitOne(ReceiveTimeout);
            if (result == null) {
                throw new TimeoutException("Timeout in synchronous request");
            } else {
                return result;
            }
        }

        public class RequestHandle {
            private readonly UInt32 SequenceNumber;
            public readonly SubProcess SubProc;
            public RequestHandle(SubProcess SubP, UInt32 SeqNum) {
                SequenceNumber = SeqNum;
                SubProc = SubP;
            }
            public void Respond(string message) {
                SubProc.PrimitiveSend(MESSAGE_TYPE_RESPONSE, SequenceNumber, message);
            }
        }

        virtual public void Dispose() {
            ReadingThread.Abort();
            FromExternal.Close();
            ToExternal.Close();
        }
    }

    public class SubProcessServer : SubProcess {
        private Process Client;
        private AnonymousPipeServerStream PipeToClient;
        private AnonymousPipeServerStream PipeFromClient;

        public SubProcessServer(string executablePath, RequestHandler handler) : base(handler) {
            PipeToClient = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            PipeFromClient = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            Initialize(PipeFromClient, PipeToClient);
            Client = new Process();
            Client.StartInfo.FileName = executablePath;
            Client.StartInfo.Arguments = PipeToClient.GetClientHandleAsString() + " " + PipeFromClient.GetClientHandleAsString();
            Client.StartInfo.UseShellExecute = false;
        }

        public override void Start() {
            Client.Start();
            PipeToClient.DisposeLocalCopyOfClientHandle();
            PipeFromClient.DisposeLocalCopyOfClientHandle();
            base.Start();
        }

        public void WaitForExit() {
            Client.WaitForExit();
        }

        override public void Dispose() {
            base.Dispose();
            PipeToClient.Dispose();
            PipeFromClient.Dispose();
            try {
                Client.Kill();
            } catch (Exception) {
                // don't care
            }
            Client.Close();
        }
    }

    public class SubProcessClient : SubProcess {
        private AnonymousPipeClientStream PipeFromServer;
        private AnonymousPipeClientStream PipeToServer;
        public SubProcessClient(string[] args, RequestHandler handler) : base(handler) {
            PipeFromServer = new AnonymousPipeClientStream(PipeDirection.In, args[0]);
            PipeToServer = new AnonymousPipeClientStream(PipeDirection.Out, args[1]);
            Initialize(PipeFromServer, PipeToServer);
        }

        override public void Dispose() {
            base.Dispose();
            PipeToServer.Dispose();
            PipeFromServer.Dispose();
        }
    }
}
