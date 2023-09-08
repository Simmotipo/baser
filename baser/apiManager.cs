using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace baser
{
    internal class apiManager
    {
        public static HttpListener listener;
        public static string url = "";
        public bool runServer = true;
        public bool deleteable = false;
        public databaseManager dbMgr;
        public string[] noServCmds = { "disableapi", "clear", "clr", "cls", "exit", "close" };


        public apiManager(ushort port, databaseManager db, string addr = "+")
        {
            dbMgr = db;
            url = $"http://{addr}:{port}/";
            // Create a Http server and start listening for incoming connections
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            Console.WriteLine("Listening for connections on {0}", url);

            // Handle requests
            Task listenTask = HandleIncomingConnections();
            //listenTask.GetAwaiter().GetResult();

        }

        public async Task HandleIncomingConnections()
        {
            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx = await listener.GetContextAsync();

                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                string request = req.Url.AbsolutePath.Substring(1);

                request = HttpUtility.UrlDecode(request);
                if (request.ToLower() != "favicon.ico")
                {
                    Console.WriteLine($"[WEB] {request}");
                    string[] result = HandleAPI(request, request.Split('/')[0]);

                    // Write the response info
                    byte[] data = Encoding.UTF8.GetBytes(result[0]);
                    resp.ContentType = result[1];
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;

                    // Write out to the response stream (asynchronously), then close it
                    await resp.OutputStream.WriteAsync(data, 0, data.Length);

                    resp.Close();
                }
            }
            // Close the listener
            listener.Close();
            deleteable = true;
        }

        public string[] HandleAPI(string cmd, string ver)
        {
            string[] result = { "", "" };
            switch(ver.ToLower())
            {
                case "api2":
                    result[1] = "application/json"; 
                    cmd = cmd.Substring(5);
                    switch (cmd.Split('/')[0].ToLower())
                    {
                        case "dumpy":
                            break;
                        case "info":
                        case "ver":
                        case "version":
                            result[0] = $"{{\"Server Version\":\"{Controller.version}\"}}";
                            break;
                        default:
                            result[0] = JsonUtils.ToJson(dbMgr.Do(cmd, "localFile"), dbMgr, includeRowNumber:true);
                            break;
                    }
                    break;
                default:
                    if (cmd.StartsWith("api/")) cmd = cmd.Substring(4);
                    if (noServCmds.Contains(cmd.Split(' ')[0].ToLower())) return new string[]{ "ERR: You have requested over API a command that can only be run locally (or on a local database).\nWhile you are here, we recommend ensuring you are running at least version 1.3.4, as this resolves some of these issues.", "text/html" };
                    else return new string[]{ dbMgr.Do(cmd, "localFile"), "text/plain" };

            }


            return result;
        }
    }
}
