namespace ircTEST
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks.Dataflow;
    using Newtonsoft.Json.Linq;
    using System.Text;
    using System.Security.Cryptography;
    using Newtonsoft.Json;
    using System.IO.Pipes;
    using System.Threading.Channels;
    using System.Text.RegularExpressions;
    using System.Net.NetworkInformation;
    using System.Xml;
    using System.Data;
    using System.Collections.Concurrent;

    /*
* This program establishes a connection to irc server, joins a channel and greets every nickname that
* joins the channel.
*
* Coded by Pasi Havia 17.11.2001 http://koti.mbnet.fi/~curupted
*/
    class IrcBot
    {
        // Irc server to connect
        public static string SERVER = "chat.freenode.net";
        // Irc server's port (6667 is default port)
        private static int PORT = 6667;
        // User information defined in RFC 2812 (Internet Relay Chat: Client Protocol) is sent to irc server
        private static string USER = "USER CSharpBot 8 * :I'm a C# irc bot";
        // Bot's nickname
        private static string NICK = "JJtestbot";

        private static string CURREENTCHANNEL = "";

        // StreamWriter is declared here so that PingSender can access it
        public static StreamWriter writer;

        private delegate void delVoidVoid();
        public delegate void delVoidString(string data);
        private static event delVoidString channelUpdate;
        public static ManualResetEvent shutdown_Event = new ManualResetEvent(false);
        public static CancellationToken CancellationToken = new CancellationToken();
        private static ircConnection mainConnection = new ircConnection(CancellationToken);

        private static System.Timers.Timer broadcasttimer;
        private static System.Timers.Timer guiTimer = new System.Timers.Timer(1000);
        public static DateTime last_suppression = DateTime.Now;
        private static bool block_checker_running = false;
      

        public static ConcurrentDictionary <string, clientStatus> clients = new ConcurrentDictionary<string, clientStatus>();
        public static bool is_connected = false;
        public static bool is_suppressed = true;

        

        static void Main(string[] args)
        {
            broadcasttimer = new System.Timers.Timer(int.Parse(args[0]));


            NICK = System.Environment.MachineName + randomEnghish.get(1).Replace(" ", "");

            channelUpdate += changeChannel;
            mainConnection.MessageReceived += MainConnection_MessageReceived;

            mainConnection.startConnect();
            while (!is_connected)
                System.Threading.Thread.Sleep(100);


            last_suppression = DateTime.Now;
            broadcasttimer.Start();
            broadcasttimer.Elapsed += Broadcasttimer_Elapsed;

            guiTimer.Start();
            guiTimer.Elapsed += GuiTimer_Elapsed;

           


           

            shutdown_Event.Reset();
            shutdown_Event.WaitOne();

            
        }

        private static void GuiTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Console.Clear();
            Console.WriteLine("JJ's IRC Connection Test bot");
            Console.WriteLine("TCP CONNECTED:" + mainConnection.irc.Connected  + " Endpoint: " + mainConnection.irc.Client.RemoteEndPoint + " Channel: " + CURREENTCHANNEL);
            
            Console.WriteLine("Client List:");
            Console.WriteLine("     *ME-> " + NICK + " Status: " +  (is_suppressed ? "Suppressed" : "Running"));
            foreach (KeyValuePair<string, clientStatus> pair in clients)
            {
                Console.WriteLine("     NAME " + pair.Key + " Status " + pair.Value.status + "       last_ping: " + pair.Value.lastPing.ToString());
            }
            clients = new ConcurrentDictionary<string, clientStatus>( clients.Where(i => i.Value.lastPing > DateTime.Now.AddSeconds(-20)).ToDictionary(x => x.Key, x => x.Value));
            
        }

      

        private static void Broadcasttimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {

            is_suppressed = (last_suppression > DateTime.Now.AddMilliseconds(-1.5 * broadcasttimer.Interval));

            if (is_suppressed) {
                mainConnection.send("Suppressed " + DateTime.Now.ToString());
            }
            else
            {
                mainConnection.send("Running " + DateTime.Now.ToString());
            }

          
            
        }

        private static void MainConnection_MessageReceived(string data)
        {
            string[] splitInput = data.Split(new Char[] { ' ' });

            if (data.Contains("Welcome") && block_checker_running == false)
            {
                is_connected = true;
                block_checker_running = true;
                Task.Factory.StartNew(() => { blockChecker(); });
            }

            if (splitInput[1] == "PRIVMSG")
            {
                string sender = splitInput[0].Split("!~")[0];
                string status = splitInput[3];
                if (clients.ContainsKey(sender))
                {
                    clients[sender].lastPing = DateTime.Now;
                    clients[sender].status = status;

                }
                else
                {
                    clients.TryAdd(sender, new clientStatus { lastPing = DateTime.Now,  status = status });
                }
            }



            
               

            if (data.Contains("Running"))
                last_suppression = DateTime.Now;

           Console.WriteLine("IRC Message " + data);
        }

        private static void changeChannel(string channel)
        {

            mainConnection.leaveChannel("#" + CURREENTCHANNEL);
            mainConnection.JoinChannel("#" + channel);
            CURREENTCHANNEL = channel;

        }



        private static void blockChecker()
        {
            


            long current_seed = 1;
            while (block_checker_running)
            {
                System.Threading.Thread.Sleep(1000);

                DateTime currentTime = DateTime.UtcNow;
                long unixTime = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();

                long seed = unixTime / 60;
                if (seed == current_seed)
                    continue;



                string hashresult = HexString2B64String(hash("vn0rjO6fmvo7rvbOoP5sJb" + seed.ToString() + "EaHQZUe3xcvn0rjO6fmvo7rvbOoP5sJbxQTU8CVhnvpJizZl9eG9S8FsDCpb8tTZ"));
                    hashresult = Regex.Replace(hashresult, "[^0-9a-zA-Z]+", "");
                    if(CURREENTCHANNEL != hashresult) {
                    channelUpdate?.Invoke(hashresult);
                }
                   

                    current_seed = seed;
                
                
            }
        }

        public static string HexString2B64String(string input)
        {

            var resultantArray = new byte[input.Length / 2];
            for (var i = 0; i < resultantArray.Length; i++)
            {
                resultantArray[i] = System.Convert.ToByte(input.Substring(i * 2, 2), 16);
            }

            return System.Convert.ToBase64String(resultantArray);
        }


        private static string hash(string data)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(data));

                // Convert byte array to a string
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }



        /*
        * Class that sends PING to irc server every 15 seconds
        */
        class PingSender
        {
            static string PING = "PING :";
            static string PONG = "PONG :";
            private Thread pingSender;
            // Empty constructor makes instance of Thread
            public PingSender()
            {
                pingSender = new Thread(new ThreadStart(this.Run));
            }
            // Starts the thread
            public void Start()
            {
                pingSender.Start();
            }
            // Send PING to irc server every 15 seconds
            public void Run()
            {
                while (true)
                {
                    IrcBot.writer.WriteLine(PING + IrcBot.SERVER);
                    IrcBot.writer.Flush();
                    Thread.Sleep(15000);
                }
            }

            public void pong()
            {
                IrcBot.writer.WriteLine(PONG + IrcBot.SERVER);
                IrcBot.writer.Flush();
                
            }

        }

        public class ircConnection
        {

            public event delVoidString MessageReceived;

            public NetworkStream stream;
            public TcpClient irc;
            string inputLine;
            StreamReader reader;
            string nickname;
            private string CHANNEL;
            PingSender ping;
            public bool is_connected = false;
            public CancellationToken cancellation ;
            public ircConnection( CancellationToken _cancellation)
            {
                cancellation = _cancellation;
                irc = new TcpClient(SERVER, PORT);

                stream = irc.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream);
                // Start PingSender thread   
            }


            public void leaveChannel(string CHANNEL)
            {

                string datastr = "PART " + CHANNEL + "\r\n";
                try
                {
                    writer.WriteLine(datastr);
                    writer.Flush();
                }
                catch {
                    Console.WriteLine("ERROR LEAVING CHANNEL " + CHANNEL);
                }
                Console.Write("WRITE " + datastr);

            }

            public void JoinChannel(string CHANNEL)
            {


                string datastr = "JOIN " + CHANNEL + "\r\n";
                try
                {
                    writer.WriteLine(datastr);
                    writer.Flush();
                }
                catch { Console.WriteLine("ERROR JOINING CHANNEL " + CHANNEL); }
                Console.Write("WRITE " + datastr);
            }



            public void startConnect()
            {
                Task.Factory.StartNew(() => { Listen(CHANNEL); });
                try
                {

                    writer.WriteLine(USER);
                    writer.Flush();
                    writer.WriteLine("NICK " + NICK + "\r\n");
                    writer.Flush();
                   
                }
                catch { }
                
                ping = new PingSender();
                ping.Start();
            }

            public void send(string data)
            {
                string datastr = " PRIVMSG #" + CURREENTCHANNEL + " :" + data + "\r\n";
                writer.WriteLine(datastr);
                writer.Flush();
                Console.Write("WRITE " + datastr);
            }


            private void Listen(string CHANNEL)
            {

                try
                {

                    while (!cancellation.IsCancellationRequested)
                    {
                        while ((inputLine = reader.ReadLine()) != null)
                        {


                            string[] splitInput = inputLine.Split(new Char[] {' '});

                            if (splitInput[0] == "PING")
                            {
                                string PongReply = splitInput[1];
                                //Console.WriteLine("->PONG " + PongReply);
                                writer.WriteLine("PONG " + PongReply);
                                writer.Flush();
                                //continue;
                            }
                            else if (inputLine.Contains("PING"))
                            {
                                ping.pong();
                            }
                            else
                                MessageReceived?.Invoke(inputLine);
                            Console.WriteLine(inputLine);
                        }
                        // Close all streams
                        writer.Close();
                        reader.Close();
                        irc.Close();
                    }
                }
                catch (Exception e)
                {
                    // Show the exception, sleep for a while and try to establish a new connection to irc server
                    Console.WriteLine(e.ToString());
                    Thread.Sleep(5000);
                    startConnect();
                    JoinChannel(CHANNEL);

                }
            }
        }



        static class randomEnghish
        {

            static String[] common = { "the", "of", "and", "a", "to", "in", "is", "you", "that", "it", "he", "was", "for", "on", "are", "as", "with", "his", "they", "I", "at", "be", "this", "have", "from", "or", "one", "had", "by", "word", "but", "not", "what", "all", "were", "we", "when", "your", "can", "said", "there", "use", "an", "each", "which", "she", "do", "how", "their", "if", "will", "up", "other", "about", "out", "many", "then", "them", "these", "so", "some", "her", "would", "make", "like", "him", "into", "time", "has", "look", "two", "more", "write", "go", "see", "number", "no", "way", "could", "people", "my", "than", "first", "water", "been", "call", "who", "oil", "its", "now", "find", "long", "down", "day", "did", "get", "come", "made", "may", "part" };

            public static string get(int count)
            {
                Random rnd = new Random();
                int rounds = rnd.Next(1, 1);
                string result = "";
                for (int i = 0; i < rounds; i++)
                {
                    ;
                    result += " " + common[rnd.Next(0, common.Length - 1)];
                }

                return result;

            }
        }

        public class clientStatus {
            public DateTime lastPing { get; set; }
            public string status { get; set; } = "";
        
        }

    }



}