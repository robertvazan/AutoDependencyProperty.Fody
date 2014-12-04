using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using System.Windows;
using System.Collections.Generic;

public class ModuleWeaver
{
    public ModuleDefinition ModuleDefinition { get; set; }

    public void Execute()
    {
        // TODO: modify getter/setter
        // TODO: condition with marker attribute
        // TODO: remove the generated backing field
        var windowsBaseRef = ModuleDefinition.AssemblyReferences.FirstOrDefault(a => a.Name == "WindowsBase");
        if (windowsBaseRef != null)
        {
            var windowsBase = ModuleDefinition.AssemblyResolver.Resolve(windowsBaseRef).MainModule;
            var mscorlib = ModuleDefinition.AssemblyResolver.Resolve(ModuleDefinition.AssemblyReferences.Single(a => a.Name == "mscorlib")).MainModule;
            var typeFromHandle = ModuleDefinition.Import(mscorlib.GetType("System.Type").Methods.Single(m => m.Name == "GetTypeFromHandle"));
            var depProperty = windowsBase.GetType("System.Windows.DependencyProperty");
            var depPropertyRef = ModuleDefinition.Import(depProperty);
            var registerSimple = ModuleDefinition.Import(depProperty.Methods.Single(m => m.Name == "Register" && m.Parameters.Count == 3));
            foreach (var type in ModuleDefinition.Types)
                if (!type.IsSpecialName && type.GenericParameters.Count == 0 && Inherits(type, "System.Windows.DependencyObject"))
                {
                    var instructions = new List<Instruction>();
                    foreach (var property in type.Properties)
                        if (!property.IsSpecialName && property.HasThis && property.GetMethod != null && property.SetMethod != null && property.GetMethod.IsPublic && property.SetMethod.IsPublic)
                        {
                            var field = new FieldDefinition(property.Name + "Property", FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Public, depPropertyRef);
                            type.Fields.Add(field);
                            instructions.Add(Instruction.Create(OpCodes.Ldstr, property.Name));
                            instructions.Add(Instruction.Create(OpCodes.Ldtoken, property.PropertyType));
                            instructions.Add(Instruction.Create(OpCodes.Call, typeFromHandle));
                            instructions.Add(Instruction.Create(OpCodes.Ldtoken, type));
                            instructions.Add(Instruction.Create(OpCodes.Call, typeFromHandle));
                            instructions.Add(Instruction.Create(OpCodes.Call, registerSimple));
                            instructions.Add(Instruction.Create(OpCodes.Stsfld, field));
                        }
                    var cctor = type.Methods.FirstOrDefault(m => m.Name == ".cctor");
                    if (cctor == null)
                    {
                        cctor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Private | MethodAttributes.RTSpecialName, ModuleDefinition.TypeSystem.Void);
                        type.Methods.Add(cctor);
                        cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                    }
                    instructions.Reverse();
                    foreach (var instruction in instructions)
                        cctor.Body.Instructions.Insert(0, instruction);
                }
        }
    }

    static bool Inherits(TypeReference type, string ancestor)
    {
        while (type != null)
        {
            if (type.FullName == ancestor)
                return true;
            type = type.Resolve().BaseType;
        }
        return false;
    }
}