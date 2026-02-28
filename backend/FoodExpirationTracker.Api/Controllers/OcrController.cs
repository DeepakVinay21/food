using FoodExpirationTracker.Api.Extensions;
using FoodExpirationTracker.Application.DTOs;
using FoodExpirationTracker.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoodExpirationTracker.Api.Controllers;

[ApiController]
[Route("api/v1/ocr")]
public class OcrController : ControllerBase
{
    private readonly OcrIngestionService _ocrIngestionService;
    private readonly OcrCorrectionService _ocrCorrectionService;

    public OcrController(OcrIngestionService ocrIngestionService, OcrCorrectionService ocrCorrectionService)
    {
        _ocrIngestionService = ocrIngestionService;
        _ocrCorrectionService = ocrCorrectionService;
    }

    [HttpPost("scan")]
    public async Task<ActionResult<ProductDto>> Scan([FromBody] OcrScanRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        var result = await _ocrIngestionService.ScanAndAddAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("scan-image-preview")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<OcrImagePreviewResponse>> ScanImagePreview([FromForm] OcrImageUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.Images is { Count: > 0 })
        {
            foreach (var file in request.Images.Take(4))
            {
                ValidateImage(file);
            }

            var imageBytes = new List<byte[]>();
            foreach (var file in request.Images.Take(4))
            {
                imageBytes.Add(await ReadBytesAsync(file, cancellationToken));
            }

            var preview = await _ocrIngestionService.ScanMultiPreviewAsync(imageBytes, cancellationToken);
            return Ok(preview);
        }

        if (request.FrontImage is not null && request.BackImage is not null)
        {
            ValidateImage(request.FrontImage);
            ValidateImage(request.BackImage);

            var front = await ReadBytesAsync(request.FrontImage, cancellationToken);
            var back = await ReadBytesAsync(request.BackImage, cancellationToken);
            var preview = await _ocrIngestionService.ScanFrontBackPreviewAsync(front, back, cancellationToken);
            return Ok(preview);
        }

        if (request.Image is null)
        {
            throw new InvalidOperationException("Image file is required.");
        }

        ValidateImage(request.Image);
        var image = await ReadBytesAsync(request.Image, cancellationToken);
        var result = await _ocrIngestionService.ScanImagePreviewAsync(image, cancellationToken);
        return Ok(result);
    }

    [HttpPost("scan-image")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<OcrImageScanResponse>> ScanImage([FromForm] OcrImageUploadRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();

        if (request.Images is { Count: > 0 })
        {
            foreach (var file in request.Images.Take(4))
            {
                ValidateImage(file);
            }

            var imageBytes = new List<byte[]>();
            foreach (var file in request.Images.Take(4))
            {
                imageBytes.Add(await ReadBytesAsync(file, cancellationToken));
            }

            var multi = await _ocrIngestionService.ScanMultiAndAddAsync(userId, imageBytes, request.Quantity, cancellationToken);
            return Ok(multi);
        }

        if (request.FrontImage is not null && request.BackImage is not null)
        {
            ValidateImage(request.FrontImage);
            ValidateImage(request.BackImage);

            var front = await ReadBytesAsync(request.FrontImage, cancellationToken);
            var back = await ReadBytesAsync(request.BackImage, cancellationToken);
            var result = await _ocrIngestionService.ScanFrontBackAndAddAsync(userId, front, back, request.Quantity, cancellationToken);
            return Ok(result);
        }

        if (request.Image is null)
        {
            throw new InvalidOperationException("Image file is required.");
        }

        ValidateImage(request.Image);
        var image = await ReadBytesAsync(request.Image, cancellationToken);

        var single = await _ocrIngestionService.ScanImageAndAddAsync(userId, image, request.Quantity, cancellationToken);
        return Ok(single);
    }

    /// <summary>
    /// Split-add: adds each detected item with its own category and expiry.
    /// </summary>
    [HttpPost("split-add")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<OcrMultiAddResponse>> SplitAdd([FromBody] SplitAddRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();

        var items = request.Items.Select(i => new DetectedItemResult(
            i.Name,
            i.CategoryName,
            DateOnly.Parse(i.ExpiryDate),
            0,
            0)).ToList();

        var preview = new OcrImagePreviewResponse(
            new OcrScanResult(request.Items[0].Name, null, DateOnly.Parse(request.Items[0].ExpiryDate), 0, request.Items[0].CategoryName, false),
            "",
            items);

        var result = await _ocrIngestionService.SplitAddAllAsync(userId, preview, request.Quantity, cancellationToken);
        return Ok(result);
    }

    [HttpPost("correct-date")]
    public async Task<IActionResult> CorrectDate([FromBody] CorrectOcrDateRequest request, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetRequiredUserId();
        await _ocrCorrectionService.CorrectExpiryDateAsync(userId, request, cancellationToken);
        return NoContent();
    }

    private static void ValidateImage(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Image file is empty.");
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only image uploads are allowed.");
        }
    }

    private static async Task<byte[]> ReadBytesAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    public class OcrImageUploadRequest
    {
        public IFormFile? Image { get; set; }
        public IFormFile? FrontImage { get; set; }
        public IFormFile? BackImage { get; set; }
        public List<IFormFile>? Images { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class SplitAddRequest
    {
        public List<SplitAddItem> Items { get; set; } = [];
        public int Quantity { get; set; } = 1;
    }

    public class SplitAddItem
    {
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "General";
        public string ExpiryDate { get; set; } = "";
    }
}
