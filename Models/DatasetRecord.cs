using System;
using System.Collections.Generic;
using LiteDB;

namespace ReLPC.Models;

public class DatasetRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<DatasetPointRecord> Points { get; set; } = [];
    public string Equation { get; set; } = "No calculation yet";
    public string Coefficient { get; set; } = "-";
    public string IntermediateComputations { get; set; } = "-";
    public List<PredictionRecord> Predictions { get; set; } = [];

    /// <summary>Single-line label for the dataset list panel.</summary>
    [BsonIgnore]
    public string ListCaption => $"{Name} · {UpdatedAt:g}";

    public override string ToString()
    {
        return $"{Name} - {UpdatedAt:g}";
    }
}

public class DatasetPointRecord
{
    public string X { get; set; } = string.Empty;
    public string Y { get; set; } = string.Empty;
}

public class PredictionRecord
{
    public string X { get; set; } = string.Empty;
    public string YPred { get; set; } = string.Empty;
    public string Y { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
