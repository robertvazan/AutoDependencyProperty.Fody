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
        var markerDll = Check(() => ModuleDefinition.AssemblyReferences.SingleOrDefault(a => a.Name.ToLowerInvariant() == "FodyDependencyPropertyMarker".ToLowerInvariant()), "find FodyDependencyPropertyMarker reference");
        if (markerDll == null)
            return;
        var windowsBase = CheckAssembly("WindowsBase");
        var mscorlib = CheckAssembly("mscorlib");
        var typeFromHandle = CheckImport(() => mscorlib.GetType("System.Type").Methods.Single(m => m.Name == "GetTypeFromHandle"), "GetTypeFromHandle");
        var depObject = Check(() => windowsBase.GetType("System.Windows.DependencyObject"), "load DependencyObject");
        var getValue = CheckImport(() => depObject.Methods.Single(m => m.Name == "GetValue"), "GetValue");
        var setValue = CheckImport(() => depObject.Methods.Single(m => m.Name == "SetValue" && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.Name == "DependencyProperty" && m.Parameters[1].ParameterType.Name == "Object"), "SetValue");
        var depProperty = Check(() => windowsBase.GetType("System.Windows.DependencyProperty"), "load DependencyProperty");
        var depPropertyRef = Check(() => ModuleDefinition.Import(depProperty), "import DependencyProperty");
        var registerSimple = CheckImport(() => depProperty.Methods.Single(m => m.Name == "Register" && m.Parameters.Count == 3), "Register");
        var registerMeta = CheckImport(() => depProperty.Methods.Single(m => m.Name == "Register" && m.Parameters.Count == 4), "Register");
        var presentationDllRef = Check(() => ModuleDefinition.AssemblyReferences.SingleOrDefault(a => a.Name.ToLowerInvariant() == "PresentationFramework".ToLowerInvariant()), "check for PresentationFramework reference");
        MethodReference metadataCtor = null;
        if (presentationDllRef != null)
        {
            var presentationDll = CheckAssembly("PresentationFramework");
            metadataCtor = CheckImport(() => presentationDll.GetType("System.Windows.FrameworkPropertyMetadata").GetConstructors().Single(c => c.Parameters.Count == 2 && c.Parameters[1].ParameterType.Name == "FrameworkPropertyMetadataOptions"), "FrameworkPropertyMetadata constructor");
        }
        foreach (var type in ModuleDefinition.Types)
            if (!type.IsSpecialName && type.GenericParameters.Count == 0 && Inherits(type, "System.Windows.DependencyObject"))
            {
                var instructions = new List<Instruction>();
                var cctor = type.GetStaticConstructor();
                foreach (var property in type.Properties)
                    if (!property.IsSpecialName && property.HasThis && property.GetMethod != null && property.SetMethod != null && property.GetMethod.IsPublic && property.SetMethod.IsPublic)
                    {
                        var attribute = property.CustomAttributes.Concat(type.CustomAttributes).FirstOrDefault(
                            a => a.AttributeType.FullName == "FodyDependencyPropertyMarker.DependencyPropertyAttribute");
                        if (attribute == null)
                            continue;
                        if (type.Fields.Any(f => f.Name == property.Name + "Property"))
                            continue;
                        var backing = type.Fields.FirstOrDefault(f => f.Name == "<" + property.Name + ">k__BackingField" && f.FieldType.FullName == property.PropertyType.FullName);
                        if (backing == null)
                            continue;
                        var field = new FieldDefinition(property.Name + "Property", FieldAttributes.Static | FieldAttributes.InitOnly | FieldAttributes.Public, depPropertyRef);
                        type.Fields.Add(field);
                        if (cctor == null)
                        {
                            cctor = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Private | MethodAttributes.RTSpecialName, ModuleDefinition.TypeSystem.Void);
                            type.Methods.Add(cctor);
                            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                        }
                        instructions.Add(Instruction.Create(OpCodes.Ldstr, property.Name));
                        instructions.Add(Instruction.Create(OpCodes.Ldtoken, property.PropertyType));
                        instructions.Add(Instruction.Create(OpCodes.Call, typeFromHandle));
                        instructions.Add(Instruction.Create(OpCodes.Ldtoken, type));
                        instructions.Add(Instruction.Create(OpCodes.Call, typeFromHandle));
                        var options = attribute.Properties.FirstOrDefault(p => p.Name == "Options");
                        if (options.Name == null || (int)options.Argument.Value == 0)
                            instructions.Add(Instruction.Create(OpCodes.Call, registerSimple));
                        else
                        {
                            if (!property.PropertyType.IsValueType)
                                instructions.Add(Instruction.Create(OpCodes.Ldnull));
                            else
                            {
                                var constVar = new VariableDefinition(property.PropertyType);
                                cctor.Body.Variables.Add(constVar);
                                instructions.Add(Instruction.Create(OpCodes.Ldloca_S, constVar));
                                instructions.Add(Instruction.Create(OpCodes.Initobj, property.PropertyType));
                                instructions.Add(Instruction.Create(OpCodes.Ldloc, constVar));
                                instructions.Add(Instruction.Create(OpCodes.Box, property.PropertyType));
                            }
                            instructions.Add(Instruction.Create(OpCodes.Ldc_I4, (int)options.Argument.Value));
                            instructions.Add(Instruction.Create(OpCodes.Newobj, metadataCtor));
                            instructions.Add(Instruction.Create(OpCodes.Call, registerMeta));
                        }
                        instructions.Add(Instruction.Create(OpCodes.Stsfld, field));
                        property.GetMethod.Body.Instructions.Clear();
                        var getter = property.GetMethod.Body.GetILProcessor();
                        getter.Emit(OpCodes.Ldarg_0);
                        getter.Emit(OpCodes.Ldarg_0);
                        getter.Emit(OpCodes.Ldfld, field);
                        getter.Emit(OpCodes.Call, getValue);
                        getter.Emit(property.PropertyType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, property.PropertyType);
                        getter.Emit(OpCodes.Ret);
                        property.SetMethod.Body.Instructions.Clear();
                        var setter = property.SetMethod.Body.GetILProcessor();
                        setter.Emit(OpCodes.Ldarg_0);
                        setter.Emit(OpCodes.Ldarg_0);
                        setter.Emit(OpCodes.Ldfld, field);
                        setter.Emit(OpCodes.Ldarg_1);
                        if (property.PropertyType.IsValueType)
                            setter.Emit(OpCodes.Box, property.PropertyType);
                        setter.Emit(OpCodes.Call, setValue);
                        setter.Emit(OpCodes.Ret);
                        type.Fields.Remove(backing);
                        property.CustomAttributes.Remove(attribute);
                    }
                instructions.Reverse();
                foreach (var instruction in instructions)
                    cctor.Body.Instructions.Insert(0, instruction);
                foreach (var attribute in type.CustomAttributes.Where(a => a.AttributeType.FullName == "FodyDependencyPropertyMarker.DependencyPropertyAttribute").ToList())
                    type.CustomAttributes.Remove(attribute);
            }
        ModuleDefinition.AssemblyReferences.Remove(markerDll);
    }

    ModuleDefinition CheckAssembly(string name)
    {
        return Check(() => ModuleDefinition.AssemblyResolver.Resolve(
            (from assembly in ModuleDefinition.AssemblyReferences
             where assembly.Name.ToLowerInvariant() == name.ToLowerInvariant()
             orderby assembly.Version descending
             select assembly).First()).MainModule, "load " + name);
    }

    MethodReference CheckImport(Func<MethodDefinition> factory, string name) { return Check(() => ModuleDefinition.Import(factory()), "import " + name); }
    static T Check<T>(Func<T> func, string message)
    {
        try
        {
            return func();
        }
        catch (Exception e)
        {
            throw new AggregateException("FodyDependencyProperty weaver failed to " + message, e);
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
