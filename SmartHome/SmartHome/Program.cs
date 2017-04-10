using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SuperSocket.SocketBase;
using SuperWebSocket;
using System.Speech.Synthesis;
using System.Net;
using System.IO;
using System.Speech.Recognition;
using Newtonsoft.Json;
using System.Speech.AudioFormat;
using NAudio.Wave;


namespace SmartHome
{
    class Program
    {         
        static SpeechStreamer streamer = new SpeechStreamer(16384);
        private static SpeechSynthesizer ss;
        private static SpeechRecognitionEngine sre;
        private static int temperature;
        private static WebSocketServer appServer = new WebSocketServer();
        //private static WaveFileWriter writer = new WaveFileWriter("recording.wav", new WaveFormat(44100, 16, 2));
        //private static MemoryStream memStream = new MemoryStream();
        private static WebSocketSession currentSession = null;
        private List<WebSocketSession> _sessions = new List<WebSocketSession>();
        private static WebSocketSession socketSession = null;
        private static WaveOut waveOut = null;
        private static BufferedWaveProvider bw = null;

        static void Main(string[] args)
        {

            Console.WriteLine("Press any key to start the WebSocketServer!");
            Console.ReadKey();
            Console.WriteLine();
            ss = new SpeechSynthesizer();
            ss.SetOutputToDefaultAudioDevice();
            //waveOut = new WaveOut();
            //bw = new BufferedWaveProvider(new WaveFormat());
            //waveOut.Init(bw);
            //waveOut.Play();

            sre = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US")); //load US English speech recognition engine
            sre.LoadGrammar(new Grammar("grammar2.xml"));
            sre.SetInputToDefaultAudioDevice();
            //sre.SetInputToAudioStream(streamer, new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Stereo));
            sre.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(sre_SpeechRecognized);

            appServer.NewMessageReceived += new SessionHandler<WebSocketSession, string>(appServer_NewMessageReceived);
            appServer.NewDataReceived += new SessionHandler<WebSocketSession, byte[]>(appServer_NewDataReceived);
            appServer.NewSessionConnected += AppServer_NewSessionConnected;
            appServer.SessionClosed += new SessionHandler<WebSocketSession, CloseReason>(appServer_SessionClosed);
            //start new task to perform asynchronous speech recognition
            Task.Factory.StartNew(() =>
            {
                sre.RecognizeAsync(RecognizeMode.Multiple);
                while (true) { }
            });
            //Setup the appServer
            if (!appServer.Setup(80)) //Setup with listening port
            {
                Console.WriteLine("Failed to setup!");
                Console.ReadKey();
                return;
            }

            //Try to start the appServer
            if (!appServer.Start())
            {
                Console.WriteLine("Failed to start!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("The server started successfully, press key 'q' to stop it!");

            while (Console.ReadKey().KeyChar != 'q')
            {
                Console.WriteLine();
                continue;
            }

            //Stop the appServer
            appServer.Stop();
            Console.WriteLine();
            Console.WriteLine("The server was stopped!");
            Console.ReadKey();
        }

        private static void AppServer_NewSessionConnected(WebSocketSession session)
        {
            Console.WriteLine("New Client connected: " + session.SessionID);
            socketSession = session;
        }

        static void appServer_SessionClosed(WebSocketSession session, CloseReason reason)
        {
            Console.WriteLine(reason);
        }

        static void appServer_NewDataReceived(WebSocketSession session, byte[] value)
        {
           // Console.WriteLine(session);                      
            streamer.Write(value, 0, value.Length);
            //bw.AddSamples(value, 0, value.Length);
        }

        static void appServer_NewMessageReceived(WebSocketSession session, string message)
        {
            //Send the received message back       
            currentSession = session;
            session.Send(message);
            Console.WriteLine("Received: " + message);
            temperature = int.Parse(message);
        }
        private static void sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            String op=null,loc=null,dev=null, method = null;
            Console.WriteLine("Detected");
            foreach (RecognizedPhrase alt in e.Result.Alternates)
            {
                Console.WriteLine("Detected: " + alt.Text + ", Confidence: " + alt.Confidence);
                Console.WriteLine(e.Result.Semantics["operation"].Value.ToString());
                Console.WriteLine(e.Result.Semantics["location"].Value.ToString());
                Console.WriteLine(e.Result.Semantics["object"].Value.ToString());
                op = e.Result.Semantics["operation"].Value.ToString();
                loc = e.Result.Semantics["location"].Value.ToString();
                dev = e.Result.Semantics["object"].Value.ToString();
                method = e.Result.Semantics["method"].Value.ToString();
            }
            float confidence = e.Result.Confidence;
            if(confidence > 0.6)
            {
                String URI = "https://smarthome295.herokuapp.com/controlDevices";
                Body body = new Body
                {
                    operation = op,
                    location = loc,
                    device = dev
                };
                Console.WriteLine(body.ToString());
                //HttpPost(URI, body.ToString(), method);
            }
            else
            {
                Console.WriteLine("Try again");
            }
        }
        public static void HttpGet(string URI)
        {
            try
            {
                WebRequest request = WebRequest.Create(URI);
                WebResponse response = request.GetResponse();
                Stream resStream = response.GetResponseStream();
                var httpResponse = (HttpWebResponse)request.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Console.WriteLine(result);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        public static void HttpPost(string URI, string data, string method)
        {
            try
            {
                string send = JsonConvert.SerializeObject(data);
                Console.WriteLine(send);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URI);
                request.Method = method;
                request.ContentType = "application/json";
                StreamWriter requestStream = new StreamWriter(request.GetRequestStream());
                requestStream.Write(data);
                requestStream.Flush();
                var httpResponse = (HttpWebResponse)request.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Console.WriteLine(result);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
    }
}
