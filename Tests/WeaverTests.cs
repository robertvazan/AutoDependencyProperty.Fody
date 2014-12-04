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
        OriginalPath = OriginalPath.Replace("Debug", "Release");
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
    public void CheckProperties()
    {
        var type = ModifiedAssembly.GetType("AssemblyToProcess.Class1");
        var myProp = (DependencyProperty)type.GetField("MyPropProperty").GetValue(null);
        var intProp = (DependencyProperty)type.GetField("IntPropProperty").GetValue(null);
        var instance = (dynamic)Activator.CreateInstance(type);
        instance.MyProp = "Hi";
        Assert.AreEqual("Hi", instance.MyProp);
        Assert.AreEqual("Hi", instance.GetValue(myProp));
        instance.SetValue(myProp, "Bye");
        Assert.AreEqual("Bye", instance.MyProp);
        Assert.AreEqual(0, instance.IntProp);
        instance.IntProp = 23;
        Assert.AreEqual(23, instance.IntProp);
        Assert.AreEqual(23, instance.GetValue(intProp));
    }

#if(DEBUG)
    [Test]
    public void PeVerify()
    {
        Verifier.Verify(OriginalPath,ModifiedPath);
    }
#endif
}