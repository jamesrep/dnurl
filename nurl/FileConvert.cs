using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

namespace nurl
{
    class FileConvert
    {
        static string STR_CRLF = "\r\n";

        public static void printTamperRequest(string strFile, string strMethod, string strUrl)
        {
            string strConverted = getRequestFromTamperData(strFile, strMethod, strUrl);

            Console.WriteLine(strConverted);
        }

        static string getRequestFromTamperData(string strFile, string strMethod, string strUrl)
        {
            string strFirst = string.Format("{0} {1} HTTP/1.1", strMethod, strUrl);
            string [] strLines  = File.ReadAllLines(strFile);

            StringBuilder sb = new StringBuilder();

            sb.Append(strFirst + STR_CRLF);

            bool bConvertMode = true;

            for (int i = 0; i < strLines.Length; i++)
            {
                strFirst = strLines[i];

                if (bConvertMode) // If we are readig the headers still
                {
                    if (strFirst.Length < 1)
                    {
                        sb.Append(STR_CRLF);
                        bConvertMode = false;
                    }

                    int eqindex = strFirst.IndexOf("=");

                    if (eqindex > 0)
                    {
                        string strTempFirst = strFirst.Substring(0, eqindex);
                        strFirst = strTempFirst + ": " + strFirst.Substring(eqindex+1, strFirst.Length - eqindex - 1);
                    }
                }

                sb.Append(strFirst + STR_CRLF);
            }

            strFirst = sb.ToString();


            return strFirst;
        }
    }
}
