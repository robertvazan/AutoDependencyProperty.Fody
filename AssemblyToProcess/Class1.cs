using FodyDependencyPropertyMarker;
using System;
using System.Windows;

namespace AssemblyToProcess
{
    public class Class1 : DependencyObject
    {
        [DependencyProperty(Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)]
        public string MyProp { get; set; }
        [DependencyProperty(Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)]
        public int IntProp { get; set; }
    }
}
