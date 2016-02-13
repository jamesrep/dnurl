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
using System.Collections.Generic;


namespace nurl
{
    class Nurl
    {           
        public string strFileName;
        public string strHost;
        public string strOutfile;
        public int port = 443;
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
        public int receiveTimeout = -1;
        public int sendTimeout = -1;
        public List <string> strReplacers = new List<string>();
        public bool bAppend = false;
        public List<Stream> outputStreams = new List<Stream>(); /// Output streams to use.

        public bool bIsChunked = false;
        public bool bIsGzip = false;
        public bool bDecodeChunked = true;

        Decoder decoder = Encoding.ASCII.GetDecoder();             

        const string STR_GZIP = "content-encoding:";
        const string STR_CHUNKED = "transfer-encoding:";
        const string STR_CONTENTLENGTH = "content-length: ";
        const string STR_CONTENTLENGTH_FIRST = "content-length:";
        

        /// <summary>
        /// Return true if strAll string indicates gzip encoded http result.
        /// </summary>
        /// <param name="strAll"></param>
        /// <returns></returns>
        static bool isGzipLine(string strAll)
        {
            return isExactHeaderStatement(strAll, STR_GZIP, "gzip");
        }

        static bool isExactHeaderStatement(string strAll, string strLeft, string strRight)
        {
            if (strAll == null) return false;
            if (strAll.ToLower().Contains(strLeft))
            {
                string[] strEncoding = strAll.Split(new char[] { ':' });

                if (strEncoding.Length > 1 && strEncoding[1].Trim().ToLower() == strRight)
                {
                    return true;
                }
            }

            return false;
        }

        static bool isContainedHeaderStatement(string strAll, string strLeft, string strRight)
        {
            if (strAll == null) return false;
            if (strAll.ToLower().Contains(strLeft))
            {
                string[] strEncoding = strAll.Split(new char[] { ':' });

                if (strEncoding.Length > 1 && strEncoding[1].Trim().ToLower().Contains(strRight))
                {
                    return true;
                }
            }

            return false;
        }
        static bool isChunkedLine(string strAll)
        {
            return isExactHeaderStatement(strAll, STR_CHUNKED, "chunked");
        }

        /// <summary>
        /// Return the content length in file.
        /// </summary>
        /// <param name="strAll"></param>
        /// <returns></returns>
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
        ///  Replaces text in body.
        /// </summary>
        /// <param name="btsToSend"></param>
        /// <returns></returns>
        byte [] replaceText(byte[] btsToSend)
        {
            string strAscii = System.Text.ASCIIEncoding.ASCII.GetString(btsToSend);

            if (this.strReplacers.Count < 1)
            {
                return btsToSend;
            }

            // TODO: make replacer-class instead.
            for(int i=0; i < strReplacers.Count; i+=2)
            {
                strAscii = strAscii.Replace(strReplacers[i], strReplacers[i + 1]);
            }

            return System.Text.ASCIIEncoding.ASCII.GetBytes(strAscii);
        }

        /// <summary>
        /// Fix the content length in bytes that are to be sent to the server.
        /// </summary>
        /// <param name="btsToSend"></param>
        /// <returns></returns>
        byte[] fixContentLength(byte [] btsToSend)
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

            return btsToSend;
        }

        byte[] receivedBytesInStream = null;
        string receivedAsciiInStream = null;
        MemoryStream memStream = new MemoryStream();

        public static byte[] readStream(Stream input)
        {
            byte[] buffer = new byte[102400];
            MemoryStream temporaryMemstream = new MemoryStream();            
            int readBytes = 0;

            input.Seek(0, SeekOrigin.Begin); // Start from beginning

            while ((readBytes = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                temporaryMemstream.Write(buffer, 0, readBytes);
            }

            return temporaryMemstream.ToArray();            
        }

        /// <summary>
        /// Get the ascii results.
        /// </summary>
        /// <returns></returns>
        public string getServerAsciiResults()
        {
            if (receivedAsciiInStream == null)
            {
                byte[] btsAll = getServerResults();

                receivedAsciiInStream = System.Text.ASCIIEncoding.ASCII.GetString(btsAll);
            }

            return receivedAsciiInStream;
        }


        /// <summary>
        /// Retrieve body
        /// </summary>
        /// <returns></returns>
        public string getServerAsciiBody()
        {
            string strReceivedAscii = getServerAsciiResults();

            if (strReceivedAscii == null) return null;

            string[] strAll = strReceivedAscii.Replace("\r", "").Split(new char[] { '\n' });

            int index = 0;

            for (int i = 0; i < strAll.Length; i++)
            {
                if (strAll[i].Length == 0)
                {

                    index = i;
                    break;
                }
            }

            StringBuilder sb = new StringBuilder();

            for (int x = index; x < strAll.Length; x++)
            {
                sb.Append(strAll[x]);
                sb.Append("\r\n");
            }

            string strBody = sb.ToString();

            return strBody;
        }

        
        /// <summary>
        /// Returns the response code (HTTP OK 200 etc.)
        /// which we assume it is the first line of the server response.
        /// </summary>
        /// <returns></returns>
        public string getResponseCode()
        {
            Hashtable htHeaders = new Hashtable();
            string strReceivedAscii = getServerAsciiResults();

            if (strReceivedAscii == null) return null;

            string[] strAll = strReceivedAscii.Replace("\r", "").Split(new char[] { '\n' });

            if (strAll == null || strAll.Length < 1) return null;

            return strAll[0];
        }

        /// <summary>
        /// Parses server response and retrieves the headers.
        /// </summary>
        /// <returns></returns>
        public Hashtable getServerHeaders()
        {
            Hashtable htHeaders = new Hashtable();
            string strReceivedAscii = getServerAsciiResults();

            if (strReceivedAscii == null) return htHeaders;

            string[] strAll = strReceivedAscii.Replace("\r", "").Split(new char[] { '\n' });

            if (strAll == null) return htHeaders;

            for (int i = 1; i < strAll.Length; i++ )
            {
                string[] strSplitted = strAll[i].Split(new char[] { ':' });
                int first = strAll[i].IndexOf(':');

                if (first > 0)
                {
                    string strA = strSplitted[0];
                    string strB = strAll[i].Substring(first + 1).TrimStart(new char[] { ' ', '\t' });
                    htHeaders.Add(strA, strB);
                }
                else
                {
                    break; // Headers end.
                }

            }

            return htHeaders;
        }

        

        /// <summary>
        /// Returns the byte array with server response.
        /// </summary>
        /// <returns></returns>
        public byte [] getServerResults()
        {
            if (receivedBytesInStream == null)
            {
                receivedBytesInStream = readStream(memStream);
            }

            return receivedBytesInStream;
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

            // Reset memory stream
            if (memStream != null && outputStreams.Contains(memStream)) outputStreams.Remove(memStream);

            // Read all bytes and prepare TCP client.
            Stream stream = null;
            TcpClient client = new TcpClient(strHost, port);
            byte[] btsToSend = File.ReadAllBytes(strFileName);

            // If we should replace parts of the file then we replace it here 
            btsToSend = replaceText(btsToSend);

            // Set timeouts
            if(this.receiveTimeout >= 0)  client.ReceiveTimeout = receiveTimeout;
            if (this.sendTimeout >= 0)  client.SendTimeout = sendTimeout;
                        
            // Parse content length and replace the current value with the proper one 
            if (bParseContentLength) btsToSend = fixContentLength(btsToSend);
           
            // If we should use an output file
            if (strOutfile != null)
            {
                FileStream sw = null;

                if (!this.bAppend)
                {
                    if (File.Exists(strOutfile))
                    {
                        File.Delete(strOutfile);
                    }

                    sw = new FileStream(strOutfile, FileMode.OpenOrCreate);                    
                }
                else
                {
                    sw = new FileStream(strOutfile, FileMode.Append);
                }

                outputStreams.Add(sw);
            }

            // If we should use SSL
            if (bIsSSL)
            {
                stream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(verifyServerCertificate), null);
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
                echoWrite(btsToSend, this.outputStreams,  true);
            }

            // Add memory stream to output buffer to fetch the server response.            
            memStream = new MemoryStream();
            this.outputStreams.Add(memStream);

            // Read message from server and write to output streams.
            string messageData = null;

            if (!this.bBinary)
            {
                messageData = readMessage(stream);
            }
            else
            {
                messageData = readMessageBinary(stream, stream);
            }

            // Write message
            Console.WriteLine(messageData);
            
            // Clean up
            client.Close();
            stream.Close();
            
        }

        /// <summary>
        /// After we are done with the object we must close all output streams.
        /// </summary>
        public void closeOutputStreams()
        {
            if (outputStreams != null)
            {
                for(int i=0; i < outputStreams.Count; i++)
                {
                    outputStreams[i].Close();
                }
            }
        }

        /// <summary>
        /// Echoes bytes both to stream and console
        /// </summary>
        /// <param name="btsToSend"></param>
        /// <param name="sw"></param>
        void echoWrite(byte[] btsToSend, List <Stream> sw, bool bWriteConsole)
        {                        
            string strLines = ASCIIEncoding.ASCII.GetString(btsToSend);
            if(bWriteConsole) Console.WriteLine(strLines);

            if (sw != null)
            {
                for (int i = 0; i < sw.Count; i++)
                {
                    sw[i].Write(btsToSend, 0, btsToSend.Length);
                }
            }            
        }


        /// <summary>
        /// Read message sent in stream. If an output file is specified; received message is stored in that file.
        /// </summary>
        /// <param name="streamFromServer"></param>
        /// <returns>The string that has been read</returns>
        string readMessage(Stream streamFromServer) 
        {
            StringBuilder messageData = new StringBuilder();            
            BinaryLineStream sr = new BinaryLineStream(streamFromServer);

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

            byte[] btsLF = ASCIIEncoding.ASCII.GetBytes(strLinefeed); // Create proper linefeed
            int contentLength = -1;

            // Get Headers.
            while (strHeaderLine != null && strHeaderLine != string.Empty)
            {
                byte[] bts = ASCIIEncoding.ASCII.GetBytes(strHeaderLine);

                // Output headers and linefeed
                echoWrite(bts, this.outputStreams, false);
                echoWrite(btsLF, this.outputStreams, false);

                if (!bSkipHeaders)
                {
                    messageData.Append(strHeaderLine);
                    messageData.Append(strLinefeed);
                }

                if (this.bDecodeGzip && isGzipLine(strHeaderLine))
                {
                    bIsGzip = true;


                }

                // If we have utf-8
                if (isContainedHeaderStatement(strHeaderLine, "content-type:", "charset=utf-8"))
                {
                    this.decoder = System.Text.UTF8Encoding.UTF8.GetDecoder();
                }

                /*
                if (!bIsChunked && bDecodeChunked)
                {
                    bIsChunked = isChunkedLine(strHeaderLine);

                    if (bIsChunked)
                    {
                        //throw new FormatException("Chunking not supported yet!");

                        if (bIsGzip)
                        {
                            streamToUse = new GZipStream(new ChunkedStream(streamFromServer), CompressionMode.Decompress);
                        }
                        else
                        {
                            streamToUse = new ChunkedStream(streamFromServer);
                        }
                    }
                }
                 */

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

            // Output linefeed
            if (strHeaderLine == string.Empty)
            {
                this.echoWrite(btsLF, outputStreams, false);
            }

            // Check content length
            if (contentLength != 0)
            {
                Stream streamToUse = streamFromServer;

                if (bIsChunked)
                {
                    if (bIsGzip)
                    {
                        streamToUse = new GZipStream(new ChunkedStream(streamFromServer), CompressionMode.Decompress);
                    }
                    else
                    {
                        streamToUse = new ChunkedStream(streamFromServer);
                    }
                }
                else if(bIsGzip)
                {
                    streamToUse = new GZipStream(streamFromServer, CompressionMode.Decompress);
                }

                // Now we are finished with headers so we read the rest of the response
                fillMessageData(streamToUse, messageData, streamFromServer);
            }

            return messageData.ToString();
        }

        string readMessageBinary(Stream stream, Stream streamFromServer)
        {
            StringBuilder messageData = new StringBuilder();
            fillMessageData(stream, messageData, streamFromServer);

            return messageData.ToString();
        }

        void fillMessageData(Stream streamToUse, StringBuilder messageData, Stream streamFromServer)
        {
            byte[] buffer = new byte[2048];
            int bytes = 0;

            try
            {
                bytes = streamToUse.Read(buffer, 0, buffer.Length);                
            }
            catch (InvalidDataException exInvalidData)
            {
                Console.WriteLine("[-] Warning!!!  " + exInvalidData.Message + ", reverting to main stream!");
                streamToUse = streamFromServer;
                bytes = streamToUse.Read(buffer, 0, buffer.Length);
            }
            catch (System.IO.IOException)
            {
                return;
            }

            while (bytes > 0)
            {
                // Write to output file if exists
                byte [] btsBuffer = new byte[bytes];
                Array.Copy(buffer, btsBuffer, btsBuffer.Length);
                this.echoWrite(buffer, this.outputStreams, false);

                // Convert to Ascii.
                
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
