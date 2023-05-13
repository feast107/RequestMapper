using System;
using System.Collections.Generic;
using System.Text;

namespace Feast.RequestMapper.Interfaces
{
    public interface ISerializer
    {
        object? Deserialize(string input, Type returnType);
    }
}
