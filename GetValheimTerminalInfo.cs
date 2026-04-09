using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            Assembly asm = Assembly.LoadFile(@"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed\assembly_valheim.dll");
            Type consoleCommandType = asm.GetType("Terminal+ConsoleCommand");
            if (consoleCommandType == null)
            {
                Type terminalType = asm.GetType("Terminal");
                consoleCommandType = terminalType.GetNestedType("ConsoleCommand", BindingFlags.Public | BindingFlags.NonPublic);
            }
            
            if (consoleCommandType != null)
            {
                Console.WriteLine("Found ConsoleCommand type!");
                foreach (var ctor in consoleCommandType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    string paramList = string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name + (p.HasDefaultValue ? " = " + (p.DefaultValue ?? "null") : "")));
                    Console.WriteLine("Constructor: ConsoleCommand(" + paramList + ")");
                }
            }
            else
            {
                Console.WriteLine("Could not find ConsoleCommand type.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
