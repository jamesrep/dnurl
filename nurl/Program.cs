﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace nurl
{
    class Program
    {
        static void printUsage()
        {
            Console.WriteLine("Usage: \n");
            Console.WriteLine("--host <host/ip>");
            Console.WriteLine("--port <port number>");
            Console.WriteLine("--request <input file>");
            Console.WriteLine("--out <output file>");
            Console.WriteLine("--ssl - Use SSL for HTTPS connections");
            Console.WriteLine("--skipheaders - If headers should be skipped");
            Console.WriteLine("--skipsent - If sent http-request should be skipped");
            Console.WriteLine("--binary - Do not try to interpret HTTP-headers");

        }

        static void Main(string[] args)
        {
            Nurl nurl = new Nurl();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--ssl")
                {
                    nurl.bIsSSL = true;
                }
                else if (args[i] == "--binary")
                {
                    nurl.bBinary = true;
                }
                else if (args[i] == "--skipheaders")
                {
                    nurl.bSkipHeaders = true;
                }
                else if (args[i] == "--skipsent")
                {
                    nurl.bEchoWrite = false;
                }
                else if (args[i] == "--nogzip")
                {
                    nurl.bDecodeGzip = false;
                }
                else if (args[i] == "--parselength")
                {
                    nurl.bUseContentLength = true;
                }
                 

                if (args.Length > (i + 1))
                {
                    if (args[i] == "--request")
                    {
                        i++;
                        nurl.strFileName = args[i];
                    }
                    else if (args[i] == "--port")
                    {
                        i++;
                        nurl.port = Convert.ToInt32(args[i]);
                    }
                    else if (args[i] == "--host")
                    {
                        i++;
                        nurl.strHost = args[i];
                    }
                    else if (args[i] == "--out")
                    {
                        i++;
                        nurl.strOutfile = args[i];
                    }
                }                
            }

            if(nurl.strHost == null)
            {
                printUsage();
                return;
            }

            nurl.run();
        }
    }
}