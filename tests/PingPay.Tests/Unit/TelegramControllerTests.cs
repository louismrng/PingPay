using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PingPay.Api.Controllers;
using PingPay.Infrastructure.Services.Telegram;
using Telegram.Bot.Types;
using Xunit;

namespace PingPay.Tests.Unit;

public class TelegramControllerTests
{
    [Fact]
    public async Task Webhook_ShouldProcessAndReturnOk()
    {
        var botServiceMock = new Mock<ITelegramBotService>();
        botServiceMock.Setup(x => x.ProcessMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new global::PingPay.Core.DTOs.WhatsApp.WhatsAppResponse { Message = "ok", Success = true });

        var senderMock = new Mock<ITelegramSenderService>();
        senderMock.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new TelegramController(botServiceMock.Object, senderMock.Object, Mock.Of<Microsoft.Extensions.Logging.ILogger<TelegramController>>());

        var update = new Update
        {
            Message = new Telegram.Bot.Types.Message
            {
                Chat = new Telegram.Bot.Types.Chat { Id = 12345 },
                Text = "help",
                From = new Telegram.Bot.Types.User { Username = "tester" }
            }
        };

        var result = await controller.Webhook(update, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
