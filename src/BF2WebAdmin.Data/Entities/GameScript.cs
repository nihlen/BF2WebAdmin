using System;

namespace BF2WebAdmin.Data.Entities;

public class GameScript
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Script { get; set; }
    public string UndoScript { get; set; }
    public bool RequiresObject { get; set; }
}