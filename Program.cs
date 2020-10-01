using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace atividade_sha_256
{
    class Program
    {
        static TcpListener server = null;
        static string mode = "";

        static void Main(string[] args)
        {
            var ip = GetLocalIPAddress();
            IPAddress ipAddress = Dns.GetHostEntry(ip).AddressList[0];
            mode = args[0];
            if (mode == "master")
            {
                var hashToFind = args[1];
                var ips = new List<string>();
                if (args.Length > 2)
                {
                    ips.Add(args[2]);
                }
                if (args.Length > 3)
                {
                    ips.Add(args[3]);
                }
                if (args.Length > 4)
                {
                    ips.Add(args[4]);
                }

                Int64 temp = 0;
                Int64 max = 999999999;
                Int64 maxNode = max / Convert.ToInt64(ips.Count);
                Console.WriteLine("Nodos configurados: " + ips.Count);
                foreach (var ipTemp in ips)
                {
                    try
                    {
                        TcpClient s = new TcpClient(ip, 8081);
                        var send = "{\"min\":" + temp + ",\"max\":" + (temp + maxNode) + ",\"hash\":\"" + hashToFind + "\",\"master\":\"" + ip + "\"}";
                        s.Client.Send(System.Text.Encoding.ASCII.GetBytes(send));
                        Console.WriteLine(ipTemp + " conectou.");
                        temp += maxNode;
                    }
                    catch (System.Exception)
                    {
                        Console.WriteLine(ipTemp + " recusou.");
                    }
                }
                Server(ipAddress, 8082);
            }
            else
            {
                Console.WriteLine("IP: " + ip);
                Server(ipAddress, 8081);
            }
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public static void Server(IPAddress ip, int port)
        {
            server = new TcpListener(ip, port);
            server.Start();
            StartListener();
        }

        public static void StartListener()
        {
            try
            {
                if (mode == "master")
                {
                    Console.WriteLine("Esperando pelo resultado...");
                    TcpClient client = server.AcceptTcpClient();
                    Thread t = new Thread(new ParameterizedThreadStart(HandleDeivce));
                    t.Start(client);
                }
                else
                {
                    Console.WriteLine("Esperando pela conexão...");
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Conectado!");
                    Thread t = new Thread(new ParameterizedThreadStart(HandleDeivce));
                    t.Start(client);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
                server.Stop();
            }
        }

        public static void HandleDeivce(Object obj)
        {
            TcpClient client = (TcpClient)obj;
            var stream = client.GetStream();
            string imei = String.Empty;
            string data = null;
            Byte[] bytes = new Byte[256];
            int i;
            try
            {
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    data = Encoding.ASCII.GetString(bytes, 0, i);
                    if (!String.IsNullOrEmpty(data))
                    {
                        if (mode == "master")
                        {
                            var receivedData = JsonConvert.DeserializeObject<dynamic>(data);
                            Console.WriteLine("Resultado encontrado: " + receivedData);
                            Environment.Exit(0);
                        }
                        else
                        {
                            var receivedData = JsonConvert.DeserializeObject<dynamic>(data);
                            var min = Convert.ToInt64(receivedData.min);
                            var max = Convert.ToInt64(receivedData.max);
                            var hashToFind = Convert.ToString(receivedData.hash);
                            var masterIp = Convert.ToString(receivedData.master);
                            var result = decript(min, max, hashToFind);
                            TcpClient s = new TcpClient(masterIp, 8082);
                            s.Client.Send(System.Text.Encoding.ASCII.GetBytes(Convert.ToString(result)));
                            Environment.Exit(0);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.ToString());
                client.Close();
            }
        }

        internal static Int64 decript(Int64 min, Int64 max, string hashToFind)
        {
            Console.WriteLine("HASH: " + hashToFind);
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "database.sqlite");
            var connectionString = string.Format("Data Source={0};Version=3;", dbPath);
            using (var con = new SQLiteConnection(connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    using (var cmd = new SQLiteCommand(con))
                    {
                        cmd.CommandText = "CREATE TABLE IF NOT EXISTS decripted (id INTEGER PRIMARY KEY AUTOINCREMENT, value text, hash varchar(64));";
                        cmd.ExecuteNonQuery();
                    }
                    transaction.Commit();
                }
            }

            using (var con = new SQLiteConnection(connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand(con))
                {
                    cmd.CommandText = "SELECT value FROM decripted WHERE hash = @hash";
                    cmd.Parameters.AddWithValue("@hash", hashToFind);
                    var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        var cachedResult = Convert.ToString(r["value"]);
                        Console.WriteLine("Encontrado em cache: " + cachedResult);
                        return Convert.ToInt64(cachedResult);
                    }
                }
            }

            using (var con = new SQLiteConnection(connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand(con))
                {
                    cmd.CommandText = "SELECT max(value) as value FROM decripted";
                    string maxCached = cmd.ExecuteScalar().ToString();
                    if (!String.IsNullOrEmpty(maxCached))
                    {
                        min = Convert.ToInt64(maxCached);
                    }
                }
            }

            Int64 result = 0;
            var superCon = new SQLiteConnection(connectionString);
            superCon.Open();
            var superTransaction = superCon.BeginTransaction();
            Console.WriteLine("Iniciando força bruta em " + min + " até " + max);
            for (Int64 i = min; i <= max; i++)
            {
                string hash = "";
                var crypt = new SHA256Managed();
                byte[] crypto = crypt.ComputeHash(Encoding.ASCII.GetBytes(i.ToString()));
                foreach (byte theByte in crypto)
                {
                    hash += theByte.ToString("x2");
                }

                using (var cmd = new SQLiteCommand(superCon))
                {
                    cmd.CommandText = "INSERT INTO decripted (value,hash) VALUES (@value, @hash)";
                    cmd.Parameters.AddWithValue("@value", i);
                    cmd.Parameters.AddWithValue("@hash", hash);
                    cmd.ExecuteNonQuery();
                }

                if (hash == hashToFind)
                {
                    result = i;
                    break;
                }
            }
            Console.WriteLine("Encontrado: " + result.ToString());
            Console.WriteLine("Persistindo cache...");
            superTransaction.Commit();
            Console.WriteLine("Concluido.");
            return result;
        }
    }
}
