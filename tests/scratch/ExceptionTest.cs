using System;
using System.IO;

class Program
{
    static void Main()
    {
        string[] testCases = {
            "\0",
            new string('a', 32768),
            "C::\\invalid",
            "C|/invalid",
            "http://invalid",
            "file:///invalid",
            "://invalid"
        };
        foreach(var t in testCases)
        {
            try
            {
                Path.GetFullPath(t);
                Console.WriteLine($"[PASS] {t.Substring(0, Math.Min(10, t.Length))}...");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"[EXCEPT] {t.Substring(0, Math.Min(10, t.Length))}... -> {ex.GetType().Name}");
            }
        }
    }
}
