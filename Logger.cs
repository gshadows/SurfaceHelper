using System;
using System.Text;
using System.IO;

namespace Observatory.SurfaceHelper {
    public static class Logger {
        private static bool headerSent = false;
        private static string Version => typeof(Logger).Assembly.GetName().Version.ToString();

        public static void AppendLog(string theMessage, string theFile) {
            if (theFile == null || theFile == "") return;
            string ret = "";
            if (!headerSent) {
                ret += $"{DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss.s")} {Version}\n";
                headerSent = true;
            }
            ret += $"{DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss.s")} {theMessage}\n";
            try {
                File.AppendAllText(theFile, ret);
            }
            catch { }
        }
    }
}
