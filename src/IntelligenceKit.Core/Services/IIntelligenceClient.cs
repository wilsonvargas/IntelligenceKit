using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Services;

public interface IIntelligenceClient
{
    Task<SendResult> SendAsync(IntelligenceEvent intelligenceEvent, CancellationToken cancellationToken = default);

    /// <summary>Uploads the screenshot blob for an already-delivered event.</summary>
    Task<SendResult> SendScreenshotAsync(Guid eventId, byte[] jpeg, CancellationToken cancellationToken = default);
}
