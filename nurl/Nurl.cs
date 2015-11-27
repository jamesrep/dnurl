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
        public bool bParseContentLength = true;
        const string STR_GZIP = "content-encoding: gzip";
        const string STR_CONTENTLENGTH = "content-length: ";
        const string STR_CONTENTLENGTH_FIRST = "content-length:";
        

        /// <summary>
        /// Return true if strAll string indicates gzip encoded http result.
        /// </summary>
        /// <param name="strAll"></param>
        /// <returns></returns>
        static bool isGzipLine(string strAll)
        {
            if(strAll == null) return false;
            if(strAll.ToLower().Contains(STR_GZIP)) return true;

            return false;
        }

        static int getContentLength(string strAll)
        {
            if (strAll == null) return -1;

            if (strAll.ToLower().Contains(STR_CONTENTLENGTH))
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

        /// <summary>
        /// Always returns true for accepting all certificates (WARNING!!!!)
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        static bool verifyServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        /// <summary>
        /// Gets the start position of post message.
        /// </summary>
        /// <param name="btsToSend"></param>
        /// <returns></returns>
        static int getPostStart(byte[] btsToSend)
        {
            if (btsToSend == null) return 0;

            int lfCount = 0;

            for (int i = 0; i < btsToSend.Length; i++)
            {
                if ('\n' == (char)btsToSend[i])
                {
                    lfCount++;

                    if (lfCount == 2)
                    {
                        return i+1;
                    }
                }
                else if('\r' != (char)btsToSend[i])
                {
                    lfCount = 0;
                }
            }

            return -1;
        }


        /// <summary>
        /// Execute the send and receive
        /// </summary>
        public void run()
        {
            if (!File.Exists(strFileName))
            {
                Console.WriteLine("[-] Error: File does not exist: " + strFileName);
                return;
            }

            // Read all bytes and prepare TCP client.
            Stream stream = null;
            byte[] btsToSend = File.ReadAllBytes(strFileName);
            TcpClient client = new TcpClient(strHost, port);
            FileStream sw = null;
            
            // Parse content length and replace the current value with the proper one 
            // TODO: Move this to a separate function.
            if (bParseContentLength)
            {
                int postStart = getPostStart(btsToSend); // Get start position of the post message.

                if (postStart >= 0) // If we have a post message
                {
                    string strAscii = System.Text.ASCIIEncoding.ASCII.GetString(btsToSend);

                    if (strAscii != null)
                    {
                        string strAscii2 = strAscii.Replace(" ", "").ToLower();

                        int lengthIndex = strAscii2.IndexOf(STR_CONTENTLENGTH_FIRST);
                        int realLengthIndex = strAscii.ToLower().IndexOf(STR_CONTENTLENGTH_FIRST);

                        if (lengthIndex >= 0)
                        {
                            int x = 0;
                            int starter = lengthIndex + STR_CONTENTLENGTH_FIRST.Length;
                            int realStarter = realLengthIndex + STR_CONTENTLENGTH_FIRST.Length + 1;

                            for (x = starter; x < strAscii2.Length; x++)
                            {
                                if (strAscii2[x] == '\r' || strAscii2[x] == '\n')
                                {
                                    break;
                                }
                            }

                            if (x > starter)
                            {
                                string strLengthText = strAscii2.Substring(starter, (x - starter));
                                int lengthRes = 0;

                                // If we have an integer we replace it with the real content length.
                                if (int.TryParse(strLengthText, out lengthRes))
                                {
                                    int realContentLength = btsToSend.Length - postStart;

                                    // We only replace if we do not have the same
                                    if (realContentLength != lengthRes)
                                    {
                                        // Get Real length bytes
                                        byte[] btsToSet = System.Text.ASCIIEncoding.ASCII.GetBytes(realContentLength.ToString());

                                        // Create final array
                                        byte[] btsFull = new byte[realStarter + btsToSet.Length + (btsToSend.Length - realStarter - strLengthText.Length)];

                                        // Part 1.
                                        byte[] btsFirst = new byte[realStarter];
                                        Array.Copy(btsToSend, btsFirst, realStarter);

                                        // Part 3.
                                        byte[] btsLast = new byte[btsFull.Length - realStarter - btsToSet.Length];
                                        Array.Copy(btsToSend, realStarter + strLengthText.Length, btsLast, 0, btsLast.Length);


                                        // Smash all parts together
                                        Array.Copy(btsFirst, btsFull, btsFirst.Length);
                                        Array.Copy(btsToSet, 0, btsFull, btsFirst.Length, btsToSet.Length);
                                        Array.Copy(btsLast, 0, btsFull, btsFirst.Length + btsToSet.Length, btsLast.Length);

                                        btsToSend = btsFull;
                                    }

                                }
                                else
                                {
                                    Console.WriteLine("[-] ERROR: Can not parse the supplied content-length: " + strLengthText);
                                }
                            }
                        }
                    }
                }
                
            }

            // If we should use an output file
            if (strOutfile != null)
            {
                if (File.Exists(strOutfile))
                {
                    File.Delete(strOutfile);
                }

                sw = new FileStream(strOutfile, FileMode.OpenOrCreate);
            }

            // If we should use SSL
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

            // Set timeouts
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
