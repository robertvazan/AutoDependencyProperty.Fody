using FodyDependencyPropertyMarker;
using System;
using System.Windows;

namespace AssemblyToProcess
{
    public class Class1 : DependencyObject
    {
        public readonly DependencyProperty AnotherProperty = DependencyProperty.Register("Another", typeof(int), typeof(Class1));
        public int Another { get { return (int)GetValue(AnotherProperty); } set { SetValue(AnotherProperty, value); } }

        [DependencyProperty]
        public string MyProp { get; set; }
        [DependencyProperty]
        public int IntProp { get; set; }
    }
}
