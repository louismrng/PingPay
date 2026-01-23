using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PingPay.Api.Controllers;
using PingPay.Infrastructure.Services.Signal;
using Xunit;

namespace PingPay.Tests.Unit;

public class SignalControllerTests
{
    [Fact]
    public async Task Webhook_ShouldProcessAndReturnOk()
    {
        var botServiceMock = new Mock<ISignalBotService>();
        botServiceMock.Setup(x => x.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new global::PingPay.Core.DTOs.WhatsApp.WhatsAppResponse { Message = "ok", Success = true });

        var senderMock = new Mock<ISignalSenderService>();
        senderMock.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new SignalController(botServiceMock.Object, senderMock.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<SignalController>>());

        var message = new SignalMessage { Source = "+14155551234", Message = "help" };

        var result = await controller.Webhook(message, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
