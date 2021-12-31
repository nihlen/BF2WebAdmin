using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BF2WebAdmin.Common.Entities.Game;
using BF2WebAdmin.DAL.Entities;
using BF2WebAdmin.DAL.Repositories;

namespace TestCore
{
    class Program
    {
        static void Main(string[] args)
        {
            //var mapConverter = new MapConverter();
            //mapConverter.ParseMaps(@"F:\Server Local\MapMod");
            //Console.ReadLine();
        }
    }

    public class MapConverter
    {
        public void ParseMaps(string directory)
        {
            Console.WriteLine("MapConverter started");
            var connectionString = @"Data Source=ALEX-PC\SQLEXPRESS;Initial Catalog=bf2;Integrated Security=true;";
            var mapRepository = new SqlMapRepository(connectionString);

            var files = Directory.GetFiles(directory);
            foreach (var filePath in files)
            {
                var now = DateTime.UtcNow;
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                var map = new MapMod
                {
                    Id = Guid.NewGuid(),
                    Name = fileName,
                    CreatedBy = "snap/krische",
                    CreatedDate = now,
                    EditedDate = now
                };

                var lines = File.ReadAllLines(filePath);
                var objects = new List<MapModObject>();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split('|');
                    objects.Add(new MapModObject
                    {
                        Id = Guid.NewGuid(),
                        MapModId = map.Id,
                        TemplateName = parts[0],
                        Position = Position.Parse(parts[1]),
                        Rotation = Rotation.Parse(parts[2])
                    });
                }

                map.Objects = objects.ToArray();
                mapRepository.Create(map);
                Console.WriteLine($"Created map {map.Name} ({map.Objects.Count()} objects)");
            }

            Console.WriteLine("Done");
        }
    }
}