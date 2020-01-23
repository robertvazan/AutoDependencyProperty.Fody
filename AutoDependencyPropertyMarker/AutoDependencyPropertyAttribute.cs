// Part of AutoDependencyProperty.Fody: https://blog.machinezoo.com/no-more-dependencyproperty-with
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace AutoDependencyPropertyMarker
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public class AutoDependencyPropertyAttribute : Attribute
    {
        public FrameworkPropertyMetadataOptions Options { get; set; }
    }
}
