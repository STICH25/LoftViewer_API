using System.Security.Claims;
using LoftViewer.Models;
using LoftViewer.Services;
using LoftViewer.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Newtonsoft.Json;

namespace LoftViewer.Controllers;

[Authorize(Roles = "Admin")]
[Route("api/birds")]
[ApiController]
public class AdminBirdsController(DbServices dbServices) : ControllerBase
{
    // Endpoint to add a new bird
    [HttpPost("addBird")]
    public async Task<IActionResult> AddBird([FromForm] Bird birdData)
    {
        var existingBird = await dbServices.FindByNameOrNumberAsync(birdData.BirdName, birdData.BirdNumber);
        if (existingBird != null)
        {
            return Conflict(new { message = "A bird with the same name or number already exists." });
        }

        var bird = new Bird
        {
            BirdName = birdData.BirdName,
            BirdNumber = birdData.BirdNumber,
            BirdColor = birdData.BirdColor ?? "N/A",
            BirdFather = birdData.BirdFather ?? "N/A",
            BirdMother = birdData.BirdMother ?? "N/A",
            Champion = birdData.Champion ?? "N/A"
        };

        if (birdData.Image != null && birdData.Image.Length > 0)
        {
            bird.ImageBytes = await ImageResizerHelper.ResizeImageAsync(birdData.Image);
        }

        await dbServices.CreateAsync(bird);
        return Ok(bird);
    }

    // Endpoint to upload bird data from JSON
    [HttpPost("upload-json")]
    public async Task<IActionResult> UploadJson(IFormFile jsonFile)
    {
        if (jsonFile == null || jsonFile.Length == 0)
        {
            return BadRequest("Invalid JSON file.");
        }

        try
        {
            using var streamReader = new StreamReader(jsonFile.OpenReadStream());
            var jsonContent = await streamReader.ReadToEndAsync();
            var birds = JsonConvert.DeserializeObject<List<Bird>>(jsonContent);

            if (birds == null || !birds.Any())
            {
                return BadRequest("The JSON file is empty or invalid.");
            }

            var addedBirds = new List<Bird>();
            var skippedBirds = new List<Bird>();

            foreach (var bird in birds)
            {
                var existingBird = await dbServices.FindByNameOrNumberAsync(bird.BirdName, bird.BirdNumber);

                if (existingBird == null)
                {
                    await dbServices.CreateAsync(bird);
                    addedBirds.Add(bird);
                }
                else
                {
                    skippedBirds.Add(bird);
                }
            }

            return Ok(new
            {
                Message = "Bird data processing completed.",
                AddedCount = addedBirds.Count,
                SkippedCount = skippedBirds.Count,
                SkippedBirds = skippedBirds.Select(b => new { b.BirdName, b.BirdNumber })
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // Endpoint to update a bird's information
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBird(string id, [FromForm] Bird updatedBird)
    {
        if (string.IsNullOrEmpty(id) || !ObjectId.TryParse(id, out _))
        {
            return BadRequest("Invalid Bird ID format.");
        }

        var existingBird = await dbServices.GetByIdAsync(id);
        if (existingBird == null)
        {
            return NotFound();
        }

        var bird = new Bird
        {
            Id = existingBird.Id,
            BirdName = updatedBird.BirdName ?? existingBird.BirdName,
            BirdNumber = updatedBird.BirdNumber ?? existingBird.BirdNumber,
            BirdColor = updatedBird.BirdColor ?? "N/A",
            BirdFather = updatedBird.BirdFather ?? "N/A",
            BirdMother = updatedBird.BirdMother ?? "N/A",
            Champion = updatedBird.Champion ?? "N/A",
            ImageBytes = updatedBird.Image != null && updatedBird.Image.Length > 0
                ? await ImageResizerHelper.ResizeImageAsync(updatedBird.Image)
                : existingBird.ImageBytes
        };

        await dbServices.UpdateAsync(id, bird);
        return NoContent();
    }

    // Endpoint to delete a bird
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBird(string id)
    {
        Console.WriteLine($"Authorization Header: {Request.Headers["Authorization"]}");

        var existingBird = await dbServices.GetByIdAsync(id);
        if (existingBird == null)
        {
            return NotFound();
        }

        await dbServices.DeleteAsync(id);
        return NoContent();
    }

    // Endpoint to upload a bird image
    [HttpPost("uploadImage")]
    public async Task<IActionResult> UploadBird([FromForm] Bird birdData)
    {
        if (birdData.Image == null || string.IsNullOrEmpty(birdData.BirdName) || string.IsNullOrEmpty(birdData.BirdNumber))
        {
            return BadRequest("Image, BirdName, and BirdNumber are required.");
        }

        if (birdData.Image != null)
        {
            var resizedImage = await ImageResizerHelper.ResizeImageAsync(birdData.Image);
            var imagePath = await ImageResizerHelper.SaveImageAsync(resizedImage);

            birdData.ImagePath = imagePath;
        }

        var bird = new Bird
        {
            BirdName = birdData.BirdName,
            BirdNumber = birdData.BirdNumber,
            ImagePath = birdData.ImagePath
        };

        await dbServices.CreateAsync(bird);
        return Ok(new { message = "Bird and image uploaded successfully." });
    }
}