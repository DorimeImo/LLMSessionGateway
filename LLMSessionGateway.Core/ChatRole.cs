using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace LLMSessionGateway.Core
{
    public enum ChatRole
    {
        User, //"user": human input
        Assistant, //"assistant": model output
        System //"system": optional instructions(e.g., behavior prompt, formatting rule)
    }
}
