﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarpetRadar.Services;
using CarpetRadar.Services.DataStorage;
using CarpetRadar.Services.IdentityServices;
using CarpetRadar.Services.Models;
using NLog;

namespace CarpetRadar.TrackServer
{
    public class CarpetTracker
    {
        private readonly TcpListener server;

        private readonly ILogger logger;
        private readonly IDataStorage dataStorage;
        private readonly IAuthenticationService authService;

        public CarpetTracker(string ip, int port)
        {
            var localAddress = IPAddress.Parse(ip);

            logger = LogManager.GetCurrentClassLogger();
            dataStorage = DataStorageBuilder.GetDefaultStorage(logger);
            authService = new AuthenticationService(dataStorage);

            server = new TcpListener(localAddress, port);
            server.Start();
            StartListener();
        }

        private void AddSomeTestData()
        {
            var r = new Random();

            var rs = new RegistrationService(dataStorage, logger);
            for (int i = 0; i < 5; i++)
            {
                var id = Guid.NewGuid().ToString("N").Substring(0, 5);
                var login = i + "i" + id;
                var userId = rs.RegisterUser(login, "password", "company " + id).Result;

                for (int f = 0; f < 4; f++)
                {
                    var flightId = Guid.NewGuid();

                    for (int j = 0; j < 20; j++)
                    {
                        int x = r.Next();
                        int y = r.Next();
                        var c = new FlightState
                        {
                            FlightId = flightId,
                            Finished = false,
                            Label = "label " + id,
                            License = "license " + id,
                            X = x,
                            Y = y
                        };
                        dataStorage.AddFlightState(c, userId.Value).GetAwaiter().GetResult();
                    }
                }
            }

            //var fs = new FlightState
            //{
            //    FlightId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            //    X = 255,
            //    Y = 256 * 256 - 1,
            //    License = Guid.Empty.ToString("N"),
            //    Token = Guid.Empty.ToString("N"),
            //    Finished = true,
            //    Label = "LABELlabelLABEL",
            //};
            //var bf = new BinaryFormatter();
            //bf.Serialize(new FileStream("flight_state_example1.bin", FileMode.Create), fs);
        }

        public void StartListener()
        {
            AddSomeTestData();

            try
            {
                while (true)
                {
                    var client = server.AcceptTcpClient();
                    logger.Info($"Connected: {client.Client.RemoteEndPoint.Serialize()}");

                    new Thread(HandleDevice).Start(client); /// как правильно делать пул
                }
            }
            catch (SocketException e)
            {
                logger.Warn(e, "SocketException");
                server.Stop();
            }
        }

        private void HandleDevice2(object obj)
        {
            var client = (TcpClient) obj;
            var stream = client.GetStream();
            var bytes = new byte[10000];
            int i;
            try
            {
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    var data = Encoding.ASCII.GetString(bytes, 0, i);
                    logger.Info($"{Thread.CurrentThread.ManagedThreadId}: Received: {data}");

                    var bf = new BinaryFormatter();
                    FlightState c;
                    using (var m = new MemoryStream(bytes))
                    {
                        c = (FlightState) bf.Deserialize(m);
                    }

                    var str = "Hey Device!";
                    var reply = Encoding.ASCII.GetBytes(str);
                    stream.Write(reply, 0, reply.Length);
                    logger.Info($"{Thread.CurrentThread.ManagedThreadId}: Sent: {str}");
                }
            }
            catch (Exception e)
            {
                logger.Warn(e, "Exception");
                client.Close();
            }
        }

        private async void HandleDevice(object obj)
        {
            var client = (TcpClient) obj;
            var stream = client.GetStream();
            try
            {
                var bf = new BinaryFormatter();
                var request = (FlightState) bf.Deserialize(stream); /// ошибка при слишком коротком стриме. надо вычитывать отдельно, чтобы форматтер знал, что больше байтов не будет

                var userId = await authService.ResolveUser(request.Token);
                if (userId == null)
                {
                    stream.Send("Authentication error.");
                    return; /// потенциальная бага))))
                }

                var errorMsg = await dataStorage.AddFlightState(request, userId.Value);
                if (errorMsg != null)
                {
                    stream.Send(errorMsg);
                    return;
                }

                var currentPositions = (await dataStorage.GetCurrentPositions()).ToArray();

                using (var ms = new MemoryStream())
                {
                    bf.Serialize(ms, currentPositions);
                    var array = ms.ToArray();
                    stream.Write(array, 0, array.Length);
                }
            }
            catch (Exception e)
            {
                logger.Warn(e, "Exception");
                logger.Warn(e.Message);
                logger.Warn(e.StackTrace);
                stream.TrySend("Internal error");
            }
            finally
            {
                client.Close();
            }
        }
    }
}