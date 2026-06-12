using System;
using System.Reflection;
using Avalonia.Controls;

class Program {
    static void Main() {
        var t = typeof(Window);
        var openedEvent = t.GetEvent("Opened");
        if (openedEvent != null) {
            Console.WriteLine("Opened event exists!");
        } else {
            Console.WriteLine("Opened event does NOT exist.");
        }
    }
}
