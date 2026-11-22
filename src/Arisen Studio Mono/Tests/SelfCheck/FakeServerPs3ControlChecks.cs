#if SELFCHECK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ArisenStudio.Services.Adapters;

namespace ArisenStudio.Tests.SelfCheck
{
    /// <summary>
    /// Self-checks for <see cref="WebManPs3ConsoleControl" /> against the local <see cref="FakeConsoleServer" /> (defined in
    /// FakeConsoleServerChecks.cs, reused here rather than copied so the type name is not declared twice). Every check starts
    /// its own fake on a free 127.0.0.1 port and stops it in finally. No real console, no user folder, no file I/O.
    /// </summary>
    internal static class FakeServerPs3ControlChecks
    {
        // The read-only request that Connect makes (GetGameAsync).
        private const string ProbeTarget = "/cpursx.ps3?/sman.ps3";

        public static void AddTo(List<SelfCheckCase> cases)
        {
            cases.Add(new SelfCheckCase("FakeServer.Ps3Control.AllowNetworkOffThrowsNoRequest", AllowNetworkOffThrowsWithoutRequest));
            cases.Add(new SelfCheckCase("FakeServer.Ps3Control.ConnectReportsConnected", ConnectReportsConnectedOnFake));
            cases.Add(new SelfCheckCase("FakeServer.Ps3Control.ClosedFakeFailsFast", ClosedFakeFailsFast));
            cases.Add(new SelfCheckCase("FakeServer.Ps3Control.NotSupportedMemberSendsNothing", NotSupportedMemberSendsNoRequest));
            cases.Add(new SelfCheckCase("FakeServer.Ps3Control.FailingBodyFreesPort", FailingBodyFreesPort));
        }

        // (1) With AllowNetwork false, Connect throws before any request reaches the fake.
        private static void AllowNetworkOffThrowsWithoutRequest()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                WebManPs3ConsoleControl control = new WebManPs3ConsoleControl(server.Address);
                // AllowNetwork is left at its default, false.

                bool threw = false;
                try
                {
                    control.Connect(server.Address);
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                SelfCheckAssert.True(threw, "Connect with AllowNetwork false did not throw InvalidOperationException");
                SelfCheckAssert.True(!control.IsConnected, "control reports connected after a refused Connect");
                SelfCheckAssert.Equal(0, server.Targets().Count, "request count with AllowNetwork false");
            }
            finally
            {
                server.Stop();
            }
        }

        // (2) With AllowNetwork true and a non-empty game answer from the fake, Connect reports connected.
        private static void ConnectReportsConnectedOnFake()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                WebManPs3ConsoleControl control = new WebManPs3ConsoleControl(server.Address);
                control.AllowNetwork = true;

                control.Connect(server.Address);

                SelfCheckAssert.True(control.IsConnected, "IsConnected was false after Connect to the fake");
                List<string> targets = server.Targets();
                SelfCheckAssert.Equal(1, targets.Count, "request count after Connect");
                SelfCheckAssert.Equal(ProbeTarget, targets[0], "probe path");
            }
            finally
            {
                server.Stop();
            }
        }

        // (3) The fake is started to get a port, then closed. Connect must throw InvalidOperationException and return
        // quickly (no hang). A connection refused on a closed port returns at once, so the bound is a few seconds.
        private static void ClosedFakeFailsFast()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            string address = server.Address;
            server.Stop();

            WebManPs3ConsoleControl control = new WebManPs3ConsoleControl(address);
            control.AllowNetwork = true;

            Stopwatch clock = Stopwatch.StartNew();
            bool threw = false;
            try
            {
                control.Connect(address);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            clock.Stop();

            SelfCheckAssert.True(threw, "Connect to a closed fake did not throw InvalidOperationException");
            SelfCheckAssert.True(!control.IsConnected, "control reports connected against a closed fake");
            SelfCheckAssert.True(clock.ElapsedMilliseconds < 5000, "Connect to a closed fake took " + clock.ElapsedMilliseconds + " ms");
        }

        // (4) NotSupported members throw NotSupportedException and send nothing. The fake is connected first, so the
        // request count is taken after Connect's own probe and any new request would show up as a change.
        private static void NotSupportedMemberSendsNoRequest()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            try
            {
                WebManPs3ConsoleControl control = new WebManPs3ConsoleControl(server.Address);
                control.AllowNetwork = true;
                control.Connect(server.Address);
                int before = server.Targets().Count;

                ExpectNotSupported(() => control.GetFirmwareVersion(), "GetFirmwareVersion");
                ExpectNotSupported(() => control.GetFirmwareType(), "GetFirmwareType");
                ExpectNotSupported(() => control.GetCoreVersion(), "GetCoreVersion");
                ExpectNotSupported(() => control.SetConsoleId("00000000000000000000000000000000"), "SetConsoleId");
                ExpectNotSupported(() => control.SetPsid("0000000000000000"), "SetPsid");
                ExpectNotSupported(() =>
                {
                    uint cell;
                    uint rsx;
                    control.GetTemperatures(out cell, out rsx);
                }, "GetTemperatures");

                SelfCheckAssert.Equal(before, server.Targets().Count, "request count after NotSupported members");
            }
            finally
            {
                server.Stop();
            }
        }

        // (5) A check that fails inside its try still runs finally, and the port it used is free afterwards.
        private static void FailingBodyFreesPort()
        {
            FakeConsoleServer server = new FakeConsoleServer();
            server.Start();
            int port = server.Port;
            bool failedAsExpected = false;

            try
            {
                try
                {
                    WebManPs3ConsoleControl control = new WebManPs3ConsoleControl(server.Address);
                    control.AllowNetwork = true;
                    control.Connect(server.Address);
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

        // Runs an action that must throw NotSupportedException.
        private static void ExpectNotSupported(Action action, string member)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new SelfCheckFailure(member + " did not throw NotSupportedException");
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
