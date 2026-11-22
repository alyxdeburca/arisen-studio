#if SELFCHECK
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ArisenStudio.Extensions;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// A minimal HTTP server on 127.0.0.1 that stands in for a console running webMAN. It listens on a free port chosen
    /// by the OS, records the request target of every request (path and query, as sent), and answers "OK" to every
    /// request except the webMAN game-info path (/cpursx.ps3), which gets a fixed HTML body. It handles one connection
    /// at a time on one background thread, and it writes nothing to disk.
    /// </summary>
    internal sealed class FakeConsoleServer
    {
        /// <summary>The game-info body: GetGameAsync reads the first h2 and returns "Fake Game Title".</summary>
        public const string GameInfoBody = "<html><body><h2>BLUS30001 Fake Game Title0123456789abc</h2></body></html>";

        private readonly TcpListener _listener;
        private readonly List<string> _targets = new List<string>();
        private readonly object _gate = new object();
        private Thread _thread;
        private volatile bool _stopping;

        public FakeConsoleServer()
        {
            // Port 0: the OS picks a free port, so two checks never share one.
            _listener = new TcpListener(IPAddress.Loopback, 0);
        }

        /// <summary>The port the server listens on. Valid after Start.</summary>
        public int Port { get; private set; }

        /// <summary>The value to pass as the webMAN "ip" argument, for example "127.0.0.1:50123".</summary>
        public string Address
        {
            get { return "127.0.0.1:" + Port; }
        }

        public void Start()
        {
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "FakeConsoleServer";
            _thread.Start();
        }

        /// <summary>Stops listening, which frees the port, and waits briefly for the thread to end. Safe to call twice.</summary>
        public void Stop()
        {
            _stopping = true;
            try
            {
                _listener.Stop();
            }
            catch (Exception)
            {
                // Already stopped.
            }

            if (_thread != null)
            {
                _thread.Join(2000);
                _thread = null;
            }
        }

        /// <summary>A copy of the request targets seen so far, in order.</summary>
        public List<string> Targets()
        {
            lock (_gate)
            {
                return new List<string>(_targets);
            }
        }

        private void Loop()
        {
            while (!_stopping)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    // Listener stopped.
                    break;
                }

                try
                {
                    Handle(client);
                }
                catch (Exception)
                {
                    // A broken client connection must not stop the server.
                }
                finally
                {
                    client.Close();
                }
            }
        }

        private void Handle(TcpClient client)
        {
            client.ReceiveTimeout = 2000;
            client.SendTimeout = 2000;
            NetworkStream stream = client.GetStream();

            string head = ReadHead(stream);
            string firstLine = head.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
            string[] parts = firstLine.Split(' ');
            string target = parts.Length >= 2 ? parts[1] : string.Empty;

            // Record before answering, so a client that has received the reply can already see the request.
            lock (_gate)
            {
                _targets.Add(target);
            }

            string body = target.StartsWith("/cpursx.ps3", StringComparison.Ordinal) ? GameInfoBody : "OK";
            string contentType = body == "OK" ? "text/plain" : "text/html";
            byte[] payload = Encoding.UTF8.GetBytes(body);

            string header =
                "HTTP/1.1 200 OK\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + payload.Length + "\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        // Reads until the blank line that ends the request head. The head is capped at 8 KB.
        private static string ReadHead(Stream stream)
        {
            StringBuilder text = new StringBuilder();
            byte[] buffer = new byte[1];
            while (text.Length < 8192)
            {
                int read = stream.Read(buffer, 0, 1);
                if (read <= 0)
                {
                    break;
                }

                text.Append((char)buffer[0]);
                if (text.Length >= 4 && text.ToString(text.Length - 4, 4) == "\r\n\r\n")
                {
                    break;
                }
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Self-checks for the webMAN helpers against <see cref="FakeConsoleServer" />. Every check uses its own fake on a
    /// free 127.0.0.1 port and a closed port for the failure cases. No real console, no user folder, no file I/O.
    /// </summary>
    internal static class FakeConsoleServerChecks
    {
        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("FakeConsole.WebManStatus.InstalledOnFake", WebManStatusTrueOnFake));
            cases.Add(new SelfCheckCase("FakeConsole.WebManStatus.ClosedPortIsTrue", WebManStatusTrueOnClosedPort));
            cases.Add(new SelfCheckCase("FakeConsole.WebManPower.SendsShutdownPath", PowerSendsShutdownPath));
            cases.Add(new SelfCheckCase("FakeConsole.WebManPower.SendsQuickRebootPath", PowerSendsQuickRebootPath));
            cases.Add(new SelfCheckCase("FakeConsole.GameInfo.ReadsTitleFromFake", GameInfoReadsTitleFromFake));
            cases.Add(new SelfCheckCase("FakeConsole.ClosedPort.ReturnsFailureNoThrow", ClosedPortReturnsFailureNoThrow));
            cases.Add(new SelfCheckCase("FakeConsole.Server.FreesPortWhenCheckFails", ServerFreesPortWhenCheckFails));
        }

        // (a1) The status check returns true against the fake, and the fake saw the LED request webMAN makes for it.
        private static void WebManStatusTrueOnFake()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                bool installed = WebManExtensions.IsWebManInstalled(server.Address);

                SelfCheckAssert.True(installed, "IsWebManInstalled against the fake returned false");
                List<string> targets = server.Targets();
                SelfCheckAssert.True(targets.Count >= 1, "the fake recorded no request");
                SelfCheckAssert.Equal("/led.ps3mapi?color=Green", targets[0], "first request path");
            }
            finally
            {
                server.Stop();
            }
        }

        // (a2) Q-054 decided: keep the OLD behaviour. With nothing listening, IsWebManInstalled returns true, because
        // HandleRequest swallows the connection error and the LED call never throws. The old app relies on this:
        // src/Arisen Studio/Extensions/PS3/WebManExtensions.cs:20-33 (return true at line 26), and the old connect at
        // src/Arisen Studio/Forms/Windows/MainWindow.cs:1160 uses the result to decide the console is webMAN-ready.
        private static void WebManStatusTrueOnClosedPort()
        {
            string address = ClosedPortAddress();

            bool installed = WebManExtensions.IsWebManInstalled(address);

            SelfCheckAssert.True(installed, "IsWebManInstalled against a closed port returned false; old app returns true (Q-054)");
        }

        // (b) A power command sends its webMAN path to the fake.
        private static void PowerSendsShutdownPath()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                WebManExtensions.Power(server.Address, WebManExtensions.PowerFlags.Shutdown);

                List<string> targets = server.Targets();
                SelfCheckAssert.Equal(1, targets.Count, "request count after Power(Shutdown)");
                SelfCheckAssert.Equal("/shutdown.ps3", targets[0], "Power(Shutdown) path");
            }
            finally
            {
                server.Stop();
            }
        }

        // (b2) A reboot variant with a query string, so the query is checked as well as the path.
        private static void PowerSendsQuickRebootPath()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                WebManExtensions.Power(server.Address, WebManExtensions.PowerFlags.QuickReboot);

                List<string> targets = server.Targets();
                SelfCheckAssert.Equal(1, targets.Count, "request count after Power(QuickReboot)");
                SelfCheckAssert.Equal("/reboot.ps3?quick", targets[0], "Power(QuickReboot) path");
            }
            finally
            {
                server.Stop();
            }
        }

        // Game info: the webMAN game-info path returns the title from the fixed body.
        private static void GameInfoReadsTitleFromFake()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                string game = GetGameOffThread(server.Address);

                SelfCheckAssert.Equal("Fake Game Title", game, "GetGameAsync title from the fake");
                List<string> targets = server.Targets();
                SelfCheckAssert.True(targets.Count >= 1 && targets[0] == "/cpursx.ps3?/sman.ps3", "game-info path was not requested");
            }
            finally
            {
                server.Stop();
            }
        }

        // (c) A closed port: the helpers do not throw, and the failure shows as the empty result of GetGameAsync.
        private static void ClosedPortReturnsFailureNoThrow()
        {
            string address = ClosedPortAddress();

            string game = GetGameOffThread(address);
            SelfCheckAssert.Equal(string.Empty, game, "GetGameAsync result on a closed port");

            // A void helper must also return normally on a closed port.
            WebManExtensions.Power(address, WebManExtensions.PowerFlags.Shutdown);
        }

        // (d) The server stops and frees its port even when the check body fails. The failure is raised inside the try,
        // so finally runs; then the same port must accept a new listener.
        private static void ServerFreesPortWhenCheckFails()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            int port = server.Port;
            bool failedAsExpected = false;

            try
            {
                try
                {
                    WebManExtensions.Power(server.Address, WebManExtensions.PowerFlags.Shutdown);
                    SelfCheckAssert.True(false, "simulated failure inside the check body");
                }
                catch (SelfCheckFailure)
                {
                    failedAsExpected = true;
                }
            }
            finally
            {
                server.Stop();
            }

            SelfCheckAssert.True(failedAsExpected, "the simulated failure did not reach the catch");
            SelfCheckAssert.True(IsPortFree(port), "port " + port + " is still held after Stop");
        }

        // Calls GetGameAsync on the thread pool and waits at most 4 s. The runner thread is a WinForms STA thread with a
        // SynchronizationContext, and GetGameAsync's await resumes on that context, so a blocking GetResult() there would
        // deadlock. Task.Run has no context, so the continuation runs on the pool. Same pattern as WebManPs3ConsoleControl.cs:65.
        private static string GetGameOffThread(string address)
        {
            Task<string> call = Task.Run<string>(() => WebManExtensions.GetGameAsync(address));
            if (!call.Wait(4000))
            {
                throw new SelfCheckFailure("GetGameAsync did not return within 4 s (no reply from the fake or a blocked call)");
            }

            return call.Result;
        }

        // Returns "127.0.0.1:port" for a port that was just released, so nothing is listening there.
        private static string ClosedPortAddress()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return "127.0.0.1:" + port;
        }

        // True when a new listener can bind the port.
        private static bool IsPortFree(int port)
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, port);
            try
            {
                probe.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                probe.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                try
                {
                    probe.Stop();
                }
                catch (Exception)
                {
                    // Not started.
                }
            }
        }
    }
}
#endif
