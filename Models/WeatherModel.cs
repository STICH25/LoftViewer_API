using System.ComponentModel.DataAnnotations;
using LoftViewer.interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoftViewer.Models;

public class WeatherModel
{
     [BsonId]
     [BsonRepresentation(BsonType.ObjectId)]
     public string? CurrentTemperature { get; set; }
     public string? WindDirection { get; set; }
     public string? WindSpeed { get; set; }
     public required string City { get; set; }
     public string? Temperature { get; set; }
     public string? Description { get; set; }
     public DateTime Timestamp { get; set; }
     
     public int Humidity { get; set; }
     
     public string? IconUrl { get; set; }
}