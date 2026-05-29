using System;
using System.Reflection;
using System.Linq;

class Probe {
static void Main() {
var asm = Assembly.LoadFrom(@"c:\Users\jonat\.nuget\packages\fido2netlib\1.0.0-alpha\lib\netstandard2.0\Fido2NetLib.dll");

void InspectType(System.Type t) {
    Console.WriteLine("TYPE: " + t.FullName + "  Base=" + t.BaseType?.Name);
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        Console.WriteLine("  FIELD(" + (f.IsStatic?"static":"inst") + "): " + f.FieldType.Name + " " + f.Name);
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        Console.WriteLine("  PROP(" + (p.GetGetMethod()?.IsStatic==true?"static":"inst") + "): " + p.PropertyType.Name + " " + p.Name);
    foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)) {
        var ps = string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine("  CTOR(" + ps + ")");
    }
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
        var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
        Console.WriteLine("  STATICMETHOD: " + m.ReturnType.Name + " " + m.Name + "(" + ps + ")");
    }
    Console.WriteLine();
}

InspectType(asm.GetType("Fido2NetLib.Objects.UserVerificationRequirement"));
InspectType(asm.GetType("Fido2NetLib.Objects.AuthenticatorAttachment"));
InspectType(asm.GetType("Fido2NetLib.Objects.AttestationConveyancePreference"));
InspectType(asm.GetType("Fido2NetLib.TypedString"));

var fido2Type = asm.GetType("Fido2NetLib.Fido2");
Console.WriteLine("=== Fido2 nested types ===");
foreach (var n in fido2Type.GetNestedTypes(BindingFlags.Public)) {
    Console.WriteLine("  NESTED: " + n.Name);
    foreach (var p in n.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine("    PROP: " + p.PropertyType.Name + " " + p.Name);
}

Console.WriteLine("=== Fido2 methods (return types) ===");
foreach (var m in fido2Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
    var ps = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
    Console.WriteLine("  " + m.ReturnType + " " + m.Name + "(" + ps + ")");
}
} }
