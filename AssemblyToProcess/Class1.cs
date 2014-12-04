using FodyDependencyPropertyMarker;
using System;
using System.Windows;

namespace AssemblyToProcess
{
    [DependencyProperty]
    public class Class1 : DependencyObject
    {
        public string MyProp { get; set; }
        public int IntProp { get; set; }
    }
}
