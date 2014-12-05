using AutoDependencyPropertyMarker;
using System;
using System.Windows;

namespace AssemblyToProcess
{
    public class Class1 : DependencyObject
    {
        [AutoDependencyProperty(Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)]
        public string MyProp { get; set; }
        [AutoDependencyProperty(Options = FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)]
        public int IntProp { get; set; }
    }
}
