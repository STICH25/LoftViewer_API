using System.Reflection;
using LoftViewer.Models;
using LoftViewer.Services;
using LoftViewer.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Newtonsoft.Json;

namespace LoftViewer.Controllers
{
    [Route("api/birds")]
    [ApiController]
    public class BirdController(DbServices birdService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAllBirds() => Ok(await birdService.GetAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBird(string id)
        {
            var bird = await birdService.GetByIdAsync(id);
            if (bird == null) return NotFound();
            return Ok(bird);
        }
        
        [HttpGet("{id}/image")]
        public async Task<IActionResult> GetBirdImage(string id)
        {
            var bird = await birdService.GetByIdAsync(id);
             var basePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException(), "images");

            // Check if the bird or its image bytes exist
            if (bird == null || bird.ImageBytes == null)
            {
                // Return a placeholder image as a fallback
                var placeholderImagePath = Path.Combine(basePath, "tempImage.jpg");
                if (System.IO.File.Exists(placeholderImagePath))
                {
                    var placeholderBytes = await System.IO.File.ReadAllBytesAsync(placeholderImagePath);
                    return File(placeholderBytes, "image/jpeg");
                }

                // If no placeholder exists, provide a fallback message
                return BadRequest(new { Message = "No image available for the specified bird ID" });
            }

            // Return the actual image if it exists
            return File(bird.ImageBytes, "image/jpeg");
        }
    }
}