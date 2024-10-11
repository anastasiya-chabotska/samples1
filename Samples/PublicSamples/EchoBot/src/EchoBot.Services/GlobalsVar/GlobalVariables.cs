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
            string filePath = @"C:\API\logs\" + myCallId +".txt";
            if (!Directory.Exists(@"C:\API\logs"))
            {
                filePath = @"C:\" + myCallId + ".txt";
            }
                //string filePath = @"D:\Control\" + myCallId + ".txt";
                if (type == 1)
            {
                if (!System.IO.File.Exists(filePath))
                {
                    System.IO.File.WriteAllText(filePath, "start writing file " + Environment.NewLine + keyAppSecret);
                }
            }
            if (type == 2)
            {
                System.IO.File.AppendAllText(filePath, "******  start bot: " + currentTime + Environment.NewLine);
                System.IO.File.AppendAllText(filePath, "           start the meet with tenant: " + tenantId + " with callId: " + myCallId + Environment.NewLine + keyAppSecret);
            }
            if (type == 3)
            {
                System.IO.File.AppendAllText(filePath, "******  end bot, summary sent: " + currentTime + Environment.NewLine + keyAppSecret);
            }
            if (type == 4)
            {
                System.IO.File.AppendAllText(filePath, "  case error:  " + currentTime + " -> " + value + Environment.NewLine + keyAppSecret);
            }
            if(type == 5)
            {
                System.IO.File.AppendAllText(filePath, "  label information:  " + currentTime + " -> " + value + Environment.NewLine + keyAppSecret);
            }
        }
        catch
        {
            
        }
    }


    public static void WriteGeneralLog(string message, string category = "General")
    {
        try
        {
            /*
            // Definir la ruta del archivo de log general
            string generalLogFilePath = @"C:\API\general_logs\GeneralLog.txt";

            // Si la ruta no existe, utilizar una ruta alternativa
            if (!Directory.Exists(@"C:\API\general_logs"))
            {
                generalLogFilePath = @"D:\GeneralLog.txt";
            }


            // Obtener la hora actual en formato UTC
            DateTime utcNow = DateTime.UtcNow;
            string currentTime = utcNow.ToString("yyyy-MM-dd HH:mm:ss");

            // Construir el mensaje a registrar
            string logMessage = $"{currentTime} [{category}]: {message}{Environment.NewLine}";

            // Registrar la información en el archivo general
            File.AppendAllText(generalLogFilePath, logMessage);*/
        }
        catch (Exception ex)
        {
            // En caso de error, imprimirlo en consola
            Console.WriteLine($"Error al escribir en el archivo de log: {ex.Message}");
        }
    }

}
