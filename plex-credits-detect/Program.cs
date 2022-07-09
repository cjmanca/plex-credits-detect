using System;
using System.Diagnostics;

internal class Root
{

}

namespace plexCreditsDetect
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Settings settings = new Settings();

            if (!settings.CheckGlobalSettingFile())
            {
                return;
            }


            settings.Load("C:\\test\\path\\to\\check\\");
        }



        public static void Exit()
        {
            Environment.Exit(0);
        }
    }
}
