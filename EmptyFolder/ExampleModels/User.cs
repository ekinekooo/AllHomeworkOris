using MigrationLibrary.Models;

namespace ExampleModels
{
    [Table("users")]
    public class User
    {
        [PrimaryKey]
        public int Id { get; set; }

        [Column]
        public string Name { get; set; }

        [Column]
        public int Age { get; set; }
    }
}