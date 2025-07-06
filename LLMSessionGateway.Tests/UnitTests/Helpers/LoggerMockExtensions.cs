
using Moq;
using Observability.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.UnitTests.Helpers
{
    public static class LoggerMockExtensions
    {
        public static void VerifyAnyLogging(this Mock<IStructuredLogger> loggerMock, Exception ex)
        {
            loggerMock.Verify(l =>
                l.LogWarning(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ex
                ), Times.AtMostOnce);

            loggerMock.Verify(l =>
                l.LogError(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ex
                ), Times.AtMostOnce);

            loggerMock.Verify(l =>
                l.LogCritical(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    ex
                ), Times.Never); // You can change this if you want to allow critical logs
        }
    }
}
