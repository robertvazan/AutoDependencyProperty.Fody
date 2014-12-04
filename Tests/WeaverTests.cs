using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using NUnit.Framework;
using System.Windows;

[TestFixture]
public class WeaverTests
{
    Assembly ModifiedAssembly;
    string ModifiedPath;
    string OriginalPath;

    [TestFixtureSetUp]
    public void Setup()
    {
        var projectPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\AssemblyToProcess\AssemblyToProcess.csproj"));
        OriginalPath = Path.Combine(Path.GetDirectoryName(projectPath), @"bin\Debug\AssemblyToProcess.dll");
#if (!DEBUG)
        assemblyPath = assemblyPath.Replace("Debug", "Release");
#endif

        ModifiedPath = OriginalPath.Replace(".dll", "2.dll");
        File.Copy(OriginalPath, ModifiedPath, true);

        var moduleDefinition = ModuleDefinition.ReadModule(ModifiedPath);
        var weavingTask = new ModuleWeaver
        {
            ModuleDefinition = moduleDefinition
        };

        weavingTask.Execute();
        moduleDefinition.Write(ModifiedPath);

        ModifiedAssembly = Assembly.LoadFile(ModifiedPath);
    }

    [Test]
    public void ValidateHelloWorldIsInjected()
    {
        var type = ModifiedAssembly.GetType("AssemblyToProcess.Class1");
        var property = (DependencyProperty)type.GetField("MyPropProperty").GetValue(null);
        var instance = (dynamic)Activator.CreateInstance(type);
        instance.MyProp = "Bye";
        Assert.AreEqual("Bye", instance.MyProp);
        Assert.AreEqual("Bye", instance.GetValue(property));
    }

#if(DEBUG)
    [Test]
    public void PeVerify()
    {
        Verifier.Verify(OriginalPath,ModifiedPath);
    }
#endif
}