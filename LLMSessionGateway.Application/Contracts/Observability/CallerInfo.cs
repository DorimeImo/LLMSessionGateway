using System.Runtime.CompilerServices;

namespace LLMSessionGateway.Application.Contracts.Observability
{
    public static class CallerInfo
    {
        public static (string Source, string Operation) GetCallerClassAndMethod(
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "")
        {
            var source = Path.GetFileNameWithoutExtension(file);
            return (source, member);
        }
    }
}
