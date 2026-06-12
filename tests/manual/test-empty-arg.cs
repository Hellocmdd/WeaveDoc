using System;
using System.IO;

class Program {
    static void Main(string[] args) {
        try {
            Console.WriteLine(Path.GetFullPath(""));
        } catch (Exception ex) {
            Console.WriteLine(ex.GetType().Name);
        }
    }
}
