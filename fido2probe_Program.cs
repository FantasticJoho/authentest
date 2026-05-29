using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            var asm = Assembly.LoadFrom(@"c:\Users\jonat\.nuget\packages\fido2netlib\1.0.0-alpha\lib\netstandard2.0\Fido2NetLib.dll");
            Console.WriteLine($"=== Fido2NetLib Assembly Info ===");
            Console.WriteLine($"FullName: {asm.FullName}");
            Console.WriteLine($"\n=== Public Types ===\n");
            
            var publicTypes = asm.GetTypes()
                .Where(t => t.IsPublic)
                .OrderBy(t => t.FullName)
                .ToList();
            
            Console.WriteLine($"Found {publicTypes.Count} public types\n");
            
            foreach (var t in publicTypes)
            {
                Console.WriteLine($"\nTYPE: {t.FullName}");
                
                // Show base type if not object
                if (t.BaseType != null && t.BaseType != typeof(object))
                {
                    Console.WriteLine($"  Base: {t.BaseType.FullName}");
                }
                
                // Show interfaces
                var interfaces = t.GetInterfaces();
                if (interfaces.Length > 0)
                {
                    Console.WriteLine($"  Implements: {string.Join(", ", interfaces.Select(i => i.FullName))}");
                }
                
                // Show properties
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OrderBy(p => p.Name);
                foreach (var p in props)
                {
                    Console.WriteLine($"  Property: {p.PropertyType.Name} {p.Name} {{ get; set; }}");
                }
                
                // Show methods
                var methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .OrderBy(m => m.Name);
                foreach (var m in methods)
                {
                    var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"  Method: {m.ReturnType.Name} {m.Name}({parms})");
                }
                
                // Show nested types
                var nestedTypes = t.GetNestedTypes(BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (var nt in nestedTypes)
                {
                    Console.WriteLine($"  Nested Type: {nt.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }
}
