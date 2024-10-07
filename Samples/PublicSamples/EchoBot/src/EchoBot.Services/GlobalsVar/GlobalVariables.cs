using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;

public static class GlobalVariables
{
    public static ConcurrentDictionary<string, ConcurrentQueue<string>> MyGlobalDictionary = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
    //public static ConcurrentQueue<string> MyGlobalQueue = new ConcurrentQueue<string>();
    public static string keyAppId;
    public static string keyAppSecret;
    public static string temporaryLanguage;
    public static ConcurrentDictionary<string, string> MyGlobalLanguage = new ConcurrentDictionary<string, string>();
    public static ConcurrentDictionary<string, string> MyGlobalUserEmail = new ConcurrentDictionary<string, string>();

    public static void writeFileControl(int type, string value = "", string myCallId = "",string tenantId="") 
    {
        try
        {
            DateTime utcNow = DateTime.UtcNow;
            string currentTime = utcNow.ToString();
            string filePath = @"C:\" + myCallId +".txt";
            if (!Directory.Exists(@"C:\"))
            {
                filePath = @"D:\Control\" + myCallId + ".txt";
            }
                //string filePath = @"D:\Control\" + myCallId + ".txt";
                if (type == 1)
            {
                if (!System.IO.File.Exists(filePath))
                {
                    System.IO.File.WriteAllText(filePath, "start writing file " + Environment.NewLine);
                }
            }
            if (type == 2)
            {
                System.IO.File.AppendAllText(filePath, "******  start bot: " + currentTime + Environment.NewLine);
                System.IO.File.AppendAllText(filePath, "           start the meet with tenant: " + tenantId + " with callId: " + myCallId + Environment.NewLine);
            }
            if (type == 3)
            {
                System.IO.File.AppendAllText(filePath, "******  end bot, summary sent: " + currentTime + Environment.NewLine);
            }
            if (type == 4)
            {
                System.IO.File.AppendAllText(filePath, "  case error:  " + currentTime + " -> " + value + Environment.NewLine);
            }
            if(type == 5)
            {
                System.IO.File.AppendAllText(filePath, "  label information:  " + currentTime + " -> " + value + Environment.NewLine);
            }
        }
        catch
        {
            
        }
    }
}
