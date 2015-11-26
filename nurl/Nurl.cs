using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.IO.Compression;


namespace nurl
{
    class Nurl
    {           
        public string strFileName;
        public string strHost;
        public string strOutfile;
        public int port;
        public int readTimeout = 5000;
        public int writeTimeout = 5000;
        public bool bIsSSL = false;
        public bool bDecodeGzip = true;
        public bool bBinary = false;
        public string strLinefeed = "\r\n";
        public bool bEchoWrite = true;
        public bool bSkipHeaders = false;
        public bool bUseContentLength = false;
        

        /// <summary>
        /// Return true if strAll string indicates gzip encoded http result.
        /// </summary>
        /// <param name="strAll"></param>
        /// <returns></returns>
        static bool isGzipLine(string strAll)
        {
            string strGzip = "Content-Encoding: gzip".ToLower();

            if(strAll == null) return false;

            if(strAll.ToLower().Contains(strGzip)) return true;

            return false;
        }

        static int getContentLength(string strAll)
        {
            string strGzip = "Content-Length: ".ToLower();


            if (strAll == null) return -1;

            if (strAll.ToLower().Contains(strGzip))
            {
                string[] strPart = strAll.Split(new char[] { ':' });
                int result = -1;

                if (strPart != null && strPart.Length > 1)
                {
                    if (int.TryParse(strPart[1], out result))
                    {
                        return result;
                    }
                }

            }

            return -1;
        }

        static bool verifyServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        /// <summary>
        /// Execute the send and receive
        /// </summary>
        public void run()
        {           
            Stream stream = null;
            byte[] btsToSend = File.ReadAllBytes(strFileName);
            TcpClient client = new TcpClient(strHost, port);
            FileStream sw = null;

            if (strOutfile != null)
            {
                if (File.Exists(strOutfile))
                {
                    File.Delete(strOutfile);
                }

                sw = new FileStream(strOutfile, FileMode.OpenOrCreate);
            }

            if (bIsSSL)
            {
                stream = new SslStream(client.GetStream(), false,
                new RemoteCertificateValidationCallback(verifyServerCertificate),
                null);
                ((SslStream) stream).AuthenticateAsClient(strHost);                

            }
            else
            {
                stream = client.GetStream();                
            }

            stream.ReadTimeout = readTimeout;
            stream.WriteTimeout = writeTimeout;

            // Send HTTP-request to host.
            stream.Write(btsToSend, 0, btsToSend.Length);


            // Write sent bytes to file and stdout
            if (bEchoWrite)
            {
                echoWrite(btsToSend, sw);
            }

            // Read message from server
            string messageData = null;

            if (!this.bBinary)
            {
                messageData = readMessage(stream, sw);
            }
            else
            {
                messageData = readMessageBinary(stream, sw);
            }

            // Write message
            Console.WriteLine(messageData);
            

            // Clean up
            client.Close();
            stream.Close();
            if (sw != null) sw.Close();
        }

        void echoWrite(byte[] btsToSend, Stream sw)
        {
            string strLines = ASCIIEncoding.ASCII.GetString(btsToSend);
            Console.WriteLine(strLines);

            if (sw != null)
            {
                sw.Write(btsToSend, 0, btsToSend.Length);
            }
        }


        /// <summary>
        /// Read message sent in stream. If an output file is specified; received message is stored in that file.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>The string that has been read</returns>
        string readMessage(Stream stream, FileStream sw)
        {
            Stream streamToUse = stream; 
            StringBuilder messageData = new StringBuilder();            
            StreamReader sr = new StreamReader(stream);

            // First read the header. If the encoding is gzip we need to decompress the result
            string strHeaderLine = null;

            try
            {
                strHeaderLine = sr.ReadLine();
            }
            catch (System.IO.IOException)
            {
                return string.Empty;
            }

            byte[] btsLF = ASCIIEncoding.ASCII.GetBytes(strLinefeed);
            int contentLength = -1;

            while (strHeaderLine != null && strHeaderLine != string.Empty)
            {
                byte[] bts = ASCIIEncoding.ASCII.GetBytes(strHeaderLine);
                if (sw != null)
                {
                    sw.Write(bts, 0, bts.Length);
                    sw.Write(btsLF, 0, btsLF.Length);
                }

                if (!bSkipHeaders)
                {
                    messageData.Append(strHeaderLine);
                    messageData.Append(strLinefeed);
                }

                if (this.bDecodeGzip && isGzipLine(strHeaderLine))
                {
                    streamToUse = new GZipStream(stream, CompressionMode.Decompress);
                }

                if (this.bUseContentLength && contentLength == -1)
                {
                    contentLength = getContentLength(strHeaderLine);
                }


                try
                {
                    strHeaderLine = sr.ReadLine();
                }
                catch (System.IO.IOException)
                {
                    break;
                }
            }

            if (strHeaderLine == string.Empty)
            {
                if (sw != null)
                {
                    sw.Write(btsLF, 0, btsLF.Length);
                }
            }

            // Check content length
            if (contentLength != 0)
            {

                // Now we are finished with headers so we read the rest of the response
                fillMessageData(streamToUse, sw, messageData);
            }

            return messageData.ToString();
        }

        string readMessageBinary(Stream stream, FileStream sw)
        {
            StringBuilder messageData = new StringBuilder();
            fillMessageData(stream, sw, messageData);

            return messageData.ToString();
        }

        void fillMessageData(Stream streamToUse, Stream sw, StringBuilder messageData)
        {
            byte[] buffer = new byte[2048];
            int bytes = 0;

            try
            {
                bytes = streamToUse.Read(buffer, 0, buffer.Length);
            }
            catch (System.IO.IOException)
            {
                return;
            }

            while (bytes > 0)
            {
                // Write to output file if exists
                if (sw != null)
                {
                    sw.Write(buffer, 0, bytes);
                }

                // Convert to Ascii.
                Decoder decoder = Encoding.ASCII.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);

                messageData.Append(chars);

                try
                {
                    bytes = streamToUse.Read(buffer, 0, buffer.Length);
                }
                catch (System.IO.IOException)
                {
                    break;
                }
            }
        }
    }
}
