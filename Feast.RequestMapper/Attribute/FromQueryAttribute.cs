using System;

namespace Feast.RequestMapper.Attribute;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class FromQueryAttribute : System.Attribute { }