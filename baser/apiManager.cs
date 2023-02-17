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
        databaseManager dbMgr;

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
                    string result = dbMgr.Do(request, "localFile");

                    // Write the response info
                    byte[] data = Encoding.UTF8.GetBytes(result);
                    resp.ContentType = "text/html";
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
    }
}
