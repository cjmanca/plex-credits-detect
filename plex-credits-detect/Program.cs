using System;
using System.Diagnostics;

namespace plexCreditsDetect
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Settings settings = new Settings();

            settings.Load("C:\\test\\path\\to\\check\\");
        }



        public static void Exit()
        {
            Environment.Exit(0);
        }
    }
}
